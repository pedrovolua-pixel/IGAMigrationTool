using System.Collections;
using System.Collections.Immutable;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AssessmentCoverage;
using AssessmentRuns;
using DeterministicAnalysis;
using Npgsql;
using SyntheticAiExecution;
using SyntheticEvaluationPopulationIntegration;
using SyntheticOutcomePriority;
using SyntheticSourceFence;

internal static partial class Program
{
    private static string Database = "";
    private static SyntheticDurableRunEngine Runs = null!;
    private static SyntheticAiExecutionStore Ai = null!;
    private static SyntheticOutcomePriorityStore Outcomes = null!;
    private static Phase1BPopulationSourceAdapter Adapter = null!;
    private static readonly AiAuthority ConsultantAi = new("synthetic-consultant", AiScope.Fixed, [AiRole.Consultant], [AiAction.Read], ["SECURITY", "OPERATIONS"]);
    private static readonly AiAuthority Worker = new("synthetic-worker", AiScope.Fixed, [AiRole.Worker], [AiAction.Read, AiAction.Dispatch, AiAction.Reconcile], ["SECURITY", "OPERATIONS"]);
    private static readonly OutcomeAuthority ConsultantOutcome = new("synthetic-consultant", true, true, false, true, OutcomeScope.Fixed, [OutcomeRole.Consultant], ["SECURITY", "OPERATIONS"], [OutcomeAction.ReadOutcome, OutcomeAction.ManageOutcome], OutcomeResourceState.Mutable);
    private static int Checks;
    internal static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException(label);
        Checks++;
        Console.WriteLine("PASS " + label);
    }
    private static async Task<int> Main()
    {
        try { await Verify(); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static async Task Verify()
    {
        Database = Environment.GetEnvironmentVariable("IGA_EVALUATION_POPULATION_INDEPENDENT_DATABASE") ?? throw new InvalidOperationException("Explicit independent database required.");
        await CreateFreshDatabase();
        Runs = new(Database, DemoFixtureCatalog.Scope, new(TimeSpan.FromMinutes(2), 3));
        Ai = new(Database);
        Outcomes = new(OutcomeScope.Fixed);
        Adapter = new(Runs, Ai, Outcomes);
        await Runs.InitializeAsync();
        await Ai.InitializeAsync();
        await using (var c = await Open()) await Outcomes.InitializeAsync(c);
        LiteralReferences();
        var normal = await Finished(2);
        await ExactSource(normal);
        await AuthorityMatrix(normal);
        await InputMatrix(normal);
        await ClosedSources();
        await GapSource(0);
        await GapSource(1);
        await Tamper(normal);
        await Drift(normal);
        await Concurrency(normal);
        Console.WriteLine($"PASS {Checks} independent assertions; actual owning PostgreSQL; no existing database reset.");
    }
    private static async Task CreateFreshDatabase()
    {
        var b = new NpgsqlConnectionStringBuilder(Database);
        if (b.Host is not ("localhost" or "127.0.0.1") || b.Port != 55433 || b.Username != "iga_synthetic" ||
            b.Database is null || !b.Database.StartsWith("iga_synthetic_phase1b_pop06_ind", StringComparison.Ordinal) ||
            b.Database.Any(ch => !char.IsAsciiLetterOrDigit(ch) && ch != '_')) throw new InvalidOperationException("Dedicated independent synthetic database required.");
        var name = b.Database;
        b.Database = "postgres";
        await using var c = new NpgsqlConnection(b.ConnectionString);
        await c.OpenAsync();
        await using var exists = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_database WHERE datname=@name)", c);
        exists.Parameters.AddWithValue("name", name);
        if ((bool)(await exists.ExecuteScalarAsync())!) throw new InvalidOperationException("Independent database must be fresh; existing data preserved.");
        await using var create = new NpgsqlCommand("CREATE DATABASE \"" + name + "\"", c);
        await create.ExecuteNonQueryAsync();
    }
    private static async Task<NpgsqlConnection> Open(string? connection = null)
    {
        var c = new NpgsqlConnection(connection ?? Database);
        await c.OpenAsync();
        return c;
    }
    private static async Task<T> Tx<T>(Guid run, Func<NpgsqlConnection, NpgsqlTransaction, Task<T>> action)
    {
        await using var c = await Open();
        await using var t = await c.BeginTransactionAsync();
        await SyntheticOutcomePriorityStore.AcquireRegistryFenceAsync(c, t, OutcomeScope.Fixed);
        if (run != Guid.Empty) await SyntheticRunSourceFence.AcquireAsync(c, t, "synthetic-customer", "synthetic-project", "synthetic-environment", run);
        var value = await action(c, t);
        await t.CommitAsync();
        return value;
    }
    private static async Task<SyntheticRunSnapshot> Started(Guid? fixedId = null)
    {
        var id = fixedId ?? Guid.NewGuid();
        return await Tx(id, async (c, t) =>
        {
            var locked = await Outcomes.LockOutcomesAsync(c, t, id, ConsultantOutcome, []);
            Check(locked.Issue is null, "owning empty desired-outcome lock saved");
            var request = DemoPhase1BCatalog.CreateStartRequest(id.ToString("D"), DemoPhase1BAiFixture.Locks(id, locked.Lock!.ContentDigest), AiExecutionPolicy.PolicyVersion, AiExecutionPolicy.PromptVersion, AiExecutionPolicy.ProviderVersion);
            var started = await Runs.StartInTransactionAsync(c, t, request, id);
            Check(started.Succeeded, "actual owning Phase1B start");
            var ai = await Ai.EnsureRunAsync(c, t, Worker, DemoPhase1BAiFixture.RunLock(started.Snapshot!), DemoPhase1BAiFixture.Works(id));
            Check(ai.Succeeded, "actual owning AI frozen work saved");
            return started.Snapshot!;
        });
    }
    private static async Task<SyntheticRunSnapshot> Finished(int proposals)
    {
        var started = await Started(proposals == 2 ? Guid.Parse("51cc48e7-65d7-4052-a247-dde748012f38") : null);
        var lease = await Runs.AcquireLeaseAsync(started.Scope, started.RunId, "synthetic-independent-worker");
        Check(lease.Succeeded, "owning coverage lease");
        var run = lease.Snapshot!;
        var generation = run.Lease!.Generation;
        var deterministic = DemoPhase1BCatalog.DeterministicPlan.ExpectedResults.ToArray();
        Check(deterministic.Length == 10, "independent literal10deterministic keys");
        var begin = await Runs.BeginWorkAsync(run.Scope, run.RunId, generation, run.Revision, deterministic.Select(x => x.Key).ToArray());
        Check(begin.Succeeded, "owning deterministic work begun");
        var saved = await Runs.CheckpointAsync(run.Scope, run.RunId, generation, begin.Snapshot!.Revision, deterministic);
        Check(saved.Succeeded, "owning deterministic checkpoint");
        var work = DemoPhase1BAiFixture.Works(run.RunId).Single();
        var reserved = await Tx(run.RunId, (c, t) => Ai.ReserveAsync(c, t, Worker, run.RunId, work.WorkId, Guid.NewGuid(), 1));
        Check(reserved.Succeeded, "actual reservation committed");
        var dispatched = await Tx(run.RunId, (c, t) => Ai.MarkDispatchedAsync(c, t, Worker, run.RunId, work.WorkId, reserved.Value!.Key, reserved.Value.WorkRevision));
        Check(dispatched.Succeeded, "actual dispatch marker committed");
        // Fixture setup only, outside any database transaction; the tested capture never calls this provider.
        var receipt = new FakeAiProvider().Dispatch(new(dispatched.Value!.Key, work.Scenario, work.PacketInputJson, work.Units, false));
        if (proposals != 2)
        {
            var node = JsonNode.Parse(receipt.OutputJson!)!;
            var array = node["proposals"]!.AsArray();
            while (array.Count > proposals) array.RemoveAt(array.Count - 1);
            var output = node.ToJsonString();
            receipt = receipt with { OutputJson = output, OutputDigest = Hash(output) };
        }
        run = saved.Snapshot!;
        begin = await Runs.BeginWorkAsync(run.Scope, run.RunId, generation, run.Revision, DemoPhase1BCatalog.AiKeys.ToArray());
        Check(begin.Succeeded, "actual AI checkpoint begun");
        var completed = await Tx(run.RunId, async (c, t) =>
        {
            var result = await Ai.CompleteAsync(c, t, Worker, run.RunId, work.WorkId, dispatched.Value.Key, receipt);
            Check(result.Succeeded && result.Value!.Outcomes.Length == 2, "actual accepted receipt complete planned partition");
            return await Runs.CheckpointInTransactionAsync(c, t, run.Scope, run.RunId, generation, begin.Snapshot!.Revision, result.Value!.Outcomes.Select(SyntheticPhase1BAnalysisAdapter.Coverage).ToArray());
        });
        Check(completed.Succeeded, "AI original/gaps and coverage commit together");
        var final = await Runs.CompleteCoverageAsync(run.Scope, run.RunId, generation, completed.Snapshot!.Revision);
        Check(final.Succeeded && final.Snapshot!.State == SyntheticRunState.Scoring, "actual Scoring complete12-keycoverage");
        return final.Snapshot!;
    }
    private static async Task<Phase1BPopulationResult> Capture(Guid id, AiAuthority? ai = null, OutcomeAuthority? outcome = null)
    {
        await using var c = await Open();
        await using var t = await c.BeginTransactionAsync();
        var captured = await Adapter.CaptureAsync(c, t, id, ai ?? ConsultantAi, outcome ?? ConsultantOutcome);
        Check(t.Connection == c && c.State == ConnectionState.Open, "caller transaction remains active");
        await t.RollbackAsync();
        return captured;
    }
    private static void Denied(Phase1BPopulationResult result, Phase1BPopulationIssue issue, string label) => Check(result.Issue == issue && result.Population is null && !result.HasPopulation, label);
    private static async Task ExactSource(SyntheticRunSnapshot run)
    {
        var before = await Rows();
        var result = await Capture(run.RunId);
        Check(result.HasPopulation, "successful actual saved-source population");
        var population = result.Population!;
        VerifyPopulation(population, run);
        using var literals = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "binding-literals.json")));
        foreach (var vector in literals.RootElement.GetProperty("vectors").EnumerateArray())
        {
            var binding = population.VersionBindings.Single(x => x.Kind.ToString() == vector.GetProperty("kind").GetString());
            Check(binding.ValueJson == vector.GetProperty("valueJson").GetString(), "preimplementation literal full source-bound bundle " + binding.Kind);
            Check(binding.Reference == vector.GetProperty("reference").GetString(), "preimplementation literal version reference " + binding.Kind);
        }
        foreach (var value in new object[] { population.Members, population.Gaps, population.VersionBindings, population.MissingVersionKinds, population.VersionBindings[0].OriginPaths, population.Members[0].Occurrences })
        {
            var list = (IList)value;
            Check(list.IsReadOnly, "nested population collection readonly");
            try { list.Clear(); throw new InvalidOperationException("mutable collection"); }
            catch (NotSupportedException) { Check(true, "collection mutation rejected"); }
        }
        Check(population.GetType().GetProperties().All(p => p.SetMethod is null), "population public properties get-only");
        Check(before == await Rows(), "all owning table rows unchanged after captures and owning reads");
        Check(!before.Contains("synthetic_review.", StringComparison.Ordinal), "population did not initialize or seed review schema");
        // A rollback alone could conceal pending DML. Commit the actual successful operation and compare every owning row.
        await using (var c = await Open())
        await using (var t = await c.BeginTransactionAsync())
        {
            var committed = await Adapter.CaptureAsync(c, t, run.RunId, ConsultantAi, ConsultantOutcome);
            Check(committed.HasPopulation && t.Connection == c, "successful public population capture leaves caller-owned active transaction");
            await t.CommitAsync();
        }
        Check(before == await Rows(), "successful population capture caller COMMIT preserves all owning table bytes");
    }
    private static async Task AuthorityMatrix(SyntheticRunSnapshot run)
    {
        var before = await Rows();
        AiAuthority[] deniedAi = [ConsultantAi with { Authenticated = false }, ConsultantAi with { Active = false }, ConsultantAi with { AssignmentActive = false }, ConsultantAi with { Revoked = true }, ConsultantAi with { AiPolicyAllowed = false }, ConsultantAi with { ResourceState = AiResourceState.Published }, ConsultantAi with { ResourceState = AiResourceState.Deleted }, ConsultantAi with { Categories = ["SECURITY"] }, ConsultantAi with { Actions = [] }, ConsultantAi with { Scope = AiScope.Fixed with { EnvironmentId = "synthetic-other" } }, ConsultantAi with { Roles = [AiRole.Auditor] }, ConsultantAi with { Roles = [AiRole.Reviewer] }, ConsultantAi with { Roles = [AiRole.Worker] }, ConsultantAi with { ActorId = "synthetic-other" }, ConsultantAi with { Roles = [AiRole.Consultant, AiRole.Worker] }, ConsultantAi with { Roles = default }, ConsultantAi with { Roles = [(AiRole)999] }];
        foreach (var ai in deniedAi) Denied(await Capture(run.RunId, ai), Phase1BPopulationIssue.Denied, "exact AI-authority denial with null capture");
        OutcomeAuthority[] deniedOutcome = [ConsultantOutcome with { Authenticated = false }, ConsultantOutcome with { Active = false }, ConsultantOutcome with { AssignmentActive = false }, ConsultantOutcome with { Revoked = true }, ConsultantOutcome with { ResourceState = OutcomeResourceState.Expired }, ConsultantOutcome with { Categories = ["OPERATIONS"] }, ConsultantOutcome with { Actions = [] }, ConsultantOutcome with { AssignedScope = OutcomeScope.Fixed with { ProjectId = "synthetic-other" } }, ConsultantOutcome with { Roles = [OutcomeRole.Auditor] }, ConsultantOutcome with { Roles = [OutcomeRole.QualifiedReviewer] }, ConsultantOutcome with { Roles = [OutcomeRole.CustomerOutcomeApprover] }, ConsultantOutcome with { ActorId = "synthetic-other" }, ConsultantOutcome with { Roles = [OutcomeRole.Consultant, OutcomeRole.CustomerOutcomeApprover] }, ConsultantOutcome with { Roles = default }, ConsultantOutcome with { Roles = [(OutcomeRole)999] }];
        foreach (var outcome in deniedOutcome) Denied(await Capture(run.RunId, outcome: outcome), Phase1BPopulationIssue.Denied, "exact outcome-authority denial with null capture");
        Check(before == await Rows(), "denied authority never writes or partial payload");
    }
    private static async Task InputMatrix(SyntheticRunSnapshot run)
    {
        await using var c = await Open();
        await using var other = await Open();
        await using var t = await c.BeginTransactionAsync();
        Denied(await Adapter.CaptureAsync(c, t, Guid.Empty, ConsultantAi, ConsultantOutcome), Phase1BPopulationIssue.InvalidInput, "empty runUUID closes");
        Denied(await Adapter.CaptureAsync(other, t, run.RunId, ConsultantAi, ConsultantOutcome), Phase1BPopulationIssue.InvalidInput, "foreign transaction binding closes");
        Denied(await Adapter.CaptureAsync(c, t, run.RunId, null!, ConsultantOutcome), Phase1BPopulationIssue.Denied, "null AIauthority closes");
        Denied(await Adapter.CaptureAsync(c, t, run.RunId, ConsultantAi, null!), Phase1BPopulationIssue.Denied, "null outcomeauthority closes");
        var foreignConfigured = new NpgsqlConnectionStringBuilder(Database) { Database = "iga_synthetic_phase1b_pop06_induncreated" };
        var foreignEngine = new SyntheticDurableRunEngine(foreignConfigured.ConnectionString, DemoFixtureCatalog.Scope, new(TimeSpan.FromMinutes(2), 3));
        var foreignAdapter = new Phase1BPopulationSourceAdapter(foreignEngine, Ai, Outcomes);
        Denied(await foreignAdapter.CaptureAsync(c, t, run.RunId, ConsultantAi, ConsultantOutcome), Phase1BPopulationIssue.InvalidInput, "configured owning connection mismatch closes without opening another database");
        await using var closed = new NpgsqlConnection(Database);
        Denied(await Adapter.CaptureAsync(closed, t, run.RunId, ConsultantAi, ConsultantOutcome), Phase1BPopulationIssue.InvalidInput, "closed connection closes");
        await t.RollbackAsync();
        Denied(await Adapter.CaptureAsync(c, t, run.RunId, ConsultantAi, ConsultantOutcome), Phase1BPopulationIssue.InvalidInput, "completed transaction closes");
        await t.DisposeAsync();
        Denied(await Adapter.CaptureAsync(c, t, run.RunId, ConsultantAi, ConsultantOutcome), Phase1BPopulationIssue.InvalidInput, "disposed transaction closes");
        var wrong = new NpgsqlConnectionStringBuilder(Database) { Database = "postgres" };
        await using var wrongConnection = await Open(wrong.ConnectionString);
        await using var wrongTransaction = await wrongConnection.BeginTransactionAsync();
        Denied(await Adapter.CaptureAsync(wrongConnection, wrongTransaction, run.RunId, ConsultantAi, ConsultantOutcome), Phase1BPopulationIssue.InvalidInput, "other namespace guard closes");
        try { _ = new Phase1BPopulationSourceAdapter(null!, Ai, Outcomes); throw new InvalidOperationException("null owner accepted"); }
        catch (ArgumentNullException) { Check(true, "null owning dependency rejected"); }
    }
    private static async Task ClosedSources()
    {
        Denied(await Capture(Guid.NewGuid()), Phase1BPopulationIssue.NotFound, "absent saved run closes");
        var planned = await Started();
        Denied(await Capture(planned.RunId), Phase1BPopulationIssue.SourceUnavailable, "Planned neverpartial source");
        var lease = await Runs.AcquireLeaseAsync(planned.Scope, planned.RunId, "synthetic-independent-worker");
        Denied(await Capture(planned.RunId), Phase1BPopulationIssue.SourceUnavailable, "Running neverpartial source");
        var cancelled = await Runs.RequestCancelAsync(planned.Scope, planned.RunId, lease.Snapshot!.Revision);
        Check(cancelled.Succeeded, "actual source cancellation requested");
        var final = await Runs.FinalizeCancellationAsync(planned.Scope, planned.RunId, cancelled.Snapshot!.Revision, lease.Snapshot.Lease!.Generation);
        Check(final.Succeeded, "actual source cancellation finalized");
        Denied(await Capture(planned.RunId), Phase1BPopulationIssue.SourceUnavailable, "Cancelled neverpartial source");
        var legacyRequest = DemoFixtureCatalog.CreateStartRequest(DemoFixtureCatalog.Baselines[0].Id, DemoFixtureCatalog.Profiles[0].Id, Guid.NewGuid().ToString("D"));
        var legacy = await Runs.StartAsync(legacyRequest);
        Check(legacy.Succeeded, "actual historical profile saved");
        Denied(await Capture(legacy.Snapshot!.RunId), Phase1BPopulationIssue.SourceUnavailable, "historical profile not Phase1B no fallback");
    }
    private static async Task GapSource(int proposals)
    {
        var run = await Finished(proposals);
        var before = await Rows();
        var capture = (await Capture(run.RunId)).Population!;
        VerifyPopulation(capture!, run);
        Check(capture is not null && capture.Members.Count == proposals && capture.Gaps.Count == 2 - proposals, "genuine partial/zero conclusion count no inventedmembers");
        foreach (var gap in capture!.Gaps)
            Check(gap.State == CoverageState.NotAssessed && gap.ReasonCode == "AI_NO_VALIDATED_CONCLUSION" && gap.Stage == "AI" && Expected.Keys.Contains(gap.CoverageKey.InventoryId), "exact allowed source gap retained separately");
        if (proposals == 1) Check(capture.Members.Single().NativeGroupId == Expected.ScheduleGroup, "actual one-proposal source retains scheduleonly");
        Check(before == await Rows(), "gap capture no writes");
    }
    private static async Task Tamper(SyntheticRunSnapshot run)
    {
        foreach (var (table, sql, issue) in new[]
        {
            ("synthetic_ai_execution.current_state", "UPDATE synthetic_ai_execution.current_state SET digest=repeat('0',64) WHERE run_id=@run", Phase1BPopulationIssue.IntegrityMismatch),
            ("synthetic_ai_execution.event_proof", "UPDATE synthetic_ai_execution.event_proof SET digest=repeat('0',64) WHERE event_id IN (SELECT event_id FROM synthetic_ai_execution.events WHERE run_id=@run)", Phase1BPopulationIssue.IntegrityMismatch),
            ("synthetic_outcome_priority.run_locks", "UPDATE synthetic_outcome_priority.run_locks SET lock_digest=repeat('0',64) WHERE run_id=@run", Phase1BPopulationIssue.IntegrityMismatch),
            ("synthetic_assessment.runs", "UPDATE synthetic_assessment.runs SET input_digest=repeat('0',64) WHERE run_id=@run", Phase1BPopulationIssue.IntegrityMismatch)
        })
        {
            var before = await Rows();
            await using var c = await Open();
            await using var t = await c.BeginTransactionAsync();
            await using (var q = new NpgsqlCommand("ALTER TABLE " + table + " DISABLE TRIGGER USER; " + sql + "; ALTER TABLE " + table + " ENABLE TRIGGER USER", c, t))
            {
                q.Parameters.AddWithValue("run", run.RunId);
                await q.ExecuteNonQueryAsync();
            }
            Denied(await Adapter.CaptureAsync(c, t, run.RunId, ConsultantAi, ConsultantOutcome), issue, "owning source tamper denied " + table);
            await t.RollbackAsync();
            Check(before == await Rows(), "tamper transaction rollback preserves original bytes");
        }
    }
    private static async Task Drift(SyntheticRunSnapshot run)
    {
        var before = await Rows();
        await using var c = await Open();
        await using var t = await c.BeginTransactionAsync();
        await using (var q = new NpgsqlCommand("ALTER TABLE synthetic_ai_execution.current_state ADD COLUMN independent_drift text", c, t)) await q.ExecuteNonQueryAsync();
        Denied(await Adapter.CaptureAsync(c, t, run.RunId, ConsultantAi, ConsultantOutcome), Phase1BPopulationIssue.MigrationDrift, "physical source schema drift closes");
        await t.RollbackAsync();
        Check(before == await Rows(), "schema drift rolled back exact original rows");
    }
    private static async Task Concurrency(SyntheticRunSnapshot normal)
    {
        await using (var c = await Open())
        await using (var t = await c.BeginTransactionAsync())
        {
            Check((await Adapter.CaptureAsync(c, t, normal.RunId, ConsultantAi, ConsultantOutcome)).Population is not null, "successful capture holds registry/runfences");
            var writer = RegistryWriter();
            var runWriterDatabase = new NpgsqlConnectionStringBuilder(Database) { ApplicationName = "synthetic-pop06-successful-run-writer" };
            var runWriterEngine = new SyntheticDurableRunEngine(runWriterDatabase.ConnectionString, DemoFixtureCatalog.Scope, new(TimeSpan.FromMinutes(2), 3));
            var runWriter = runWriterEngine.RequestCancelAsync(normal.Scope, normal.RunId, normal.Revision);
            await WaitForAdvisory("synthetic-independent-registry-writer");
            Check(!writer.IsCompleted, "actual registry owner writer blocks while successful capture lives");
            await WaitForAdvisory("synthetic-pop06-successful-run-writer");
            Check(!runWriter.IsCompleted, "actual run owner writer blocks while successful Scoring population capture lives");
            await t.RollbackAsync();
            Check((await writer).Issue is null, "registry writer proceeds commits after caller rollback");
            Check((await runWriter).Issue == SyntheticRunIssue.InvalidState, "successful-capture run writer resumes with native Scoring cancellation denial");
        }
        var planned = await Started();
        await using (var c = await Open())
        await using (var t = await c.BeginTransactionAsync())
        {
            Denied(await Adapter.CaptureAsync(c, t, planned.RunId, ConsultantAi, ConsultantOutcome), Phase1BPopulationIssue.SourceUnavailable, "Planned capture denied but caller owns heldfences");
            var writerDatabase = new NpgsqlConnectionStringBuilder(Database) { ApplicationName = "synthetic-independent-run-writer" };
            var engine = new SyntheticDurableRunEngine(writerDatabase.ConnectionString, DemoFixtureCatalog.Scope, new(TimeSpan.FromMinutes(2), 3));
            var writer = engine.RequestCancelAsync(planned.Scope, planned.RunId, planned.Revision);
            await WaitForAdvisory("synthetic-independent-run-writer");
            Check(!writer.IsCompleted, "actual run source writer blocked by run fence");
            await t.RollbackAsync();
            Check((await writer).Succeeded, "run owner cancellation commits after capture rollback");
        }
        await using (var c = await Open())
        await using (var t = await c.BeginTransactionAsync())
        {
            await SyntheticRunSourceFence.AcquireAsync(c, t, "synthetic-customer", "synthetic-project", "synthetic-environment", normal.RunId);
            Denied(await Adapter.CaptureAsync(c, t, normal.RunId, ConsultantAi, ConsultantOutcome), Phase1BPopulationIssue.InvalidInput, "reversed run-before-registry rejected no deadlock");
            await t.RollbackAsync();
        }
        await using (var c = await Open())
        await using (var t = await c.BeginTransactionAsync())
        {
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            try { await Adapter.CaptureAsync(c, t, normal.RunId, ConsultantAi, ConsultantOutcome, cancelled.Token); throw new InvalidOperationException("cancellation ignored"); }
            catch (OperationCanceledException) { Check(true, "cancelled capture propagates cancellation"); }
            await t.RollbackAsync();
        }
        Check((await Capture(normal.RunId)).Population is not null, "source remains coherent/readable after concurrent writers and cancellation");
    }
    private static async Task<OutcomeApplyResult> RegistryWriter()
    {
        var b = new NpgsqlConnectionStringBuilder(Database) { ApplicationName = "synthetic-independent-registry-writer" };
        await using var c = await Open(b.ConnectionString);
        await using var t = await c.BeginTransactionAsync();
        var registry = await Outcomes.ReadRegistryAsync(c, t, ConsultantOutcome);
        var content = OutcomePriorityCanonical.Seal(new OutcomeContentVersion("independent-objective", 1, "OPERATIONS", "Fictional independent objective", "Fictional source stability", OutcomeOrigin.Documented, [new("synthetic-ai-schedule", "configuration")], ["synthetic-reference"], [], null, ""));
        var result = await Outcomes.ApplyOutcomeAsync(c, t, ConsultantOutcome, new(Guid.NewGuid(), OutcomeKind.CreateDraft, content.OutcomeId, 1, 0, content.ContentDigest, registry.Snapshot!.Revision, null, content, "Fictional independent fence check"));
        await t.CommitAsync();
        return result;
    }
    private static async Task WaitForAdvisory(string application)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            await using var c = await Open();
            await using var q = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_stat_activity WHERE application_name=@application AND wait_event_type='Lock' AND wait_event='advisory')", c);
            q.Parameters.AddWithValue("application", application);
            if ((bool)(await q.ExecuteScalarAsync())!) { Check(true, "actual PostgreSQL advisory wait observed"); return; }
            await Task.Delay(25);
        }
        throw new InvalidOperationException("expected source writer advisory wait not observed");
    }
    private static async Task<string> Rows()
    {
        await using var c = await Open();
        var tables = new List<(string Schema, string Table)>();
        await using (var q = new NpgsqlCommand("SELECT table_schema,table_name FROM information_schema.tables WHERE table_schema LIKE 'synthetic_%' AND table_type='BASE TABLE' ORDER BY table_schema,table_name", c))
        await using (var r = await q.ExecuteReaderAsync()) while (await r.ReadAsync()) tables.Add((r.GetString(0), r.GetString(1)));
        var values = new StringBuilder();
        foreach (var (schema, table) in tables)
        {
            var identifier = '"' + schema.Replace("\"", "\"\"") + "\".\"" + table.Replace("\"", "\"\"") + '"';
            await using var q = new NpgsqlCommand("SELECT row_to_json(t)::text FROM " + identifier + " t ORDER BY row_to_json(t)::text COLLATE \"C\"", c);
            await using var r = await q.ExecuteReaderAsync();
            values.Append(schema).Append('.').Append(table).Append('\n');
            while (await r.ReadAsync()) values.Append(r.GetString(0)).Append('\n');
        }
        return values.ToString();
    }
    internal static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    internal static string Canonical(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, element);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
    private static void Write(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var item in value.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal)) { writer.WritePropertyName(item.Name); Write(writer, item.Value); }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray()) Write(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(value.GetDecimal().ToString("G29", System.Globalization.CultureInfo.InvariantCulture));
                break;
            default: value.WriteTo(writer); break;
        }
    }
}
