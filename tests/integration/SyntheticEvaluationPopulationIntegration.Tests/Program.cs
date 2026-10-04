using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AssessmentCoverage;
using AssessmentRuns;
using DeterministicAnalysis;
using SyntheticEvaluation;
using SyntheticEvaluationPopulationIntegration;
using Npgsql;
using SyntheticAiExecution;
using SyntheticEvaluationSourceIntegration;
using SyntheticOutcomePriority;
using SyntheticSourceFence;

internal static class Program
{
    private static readonly string Connection = Environment.GetEnvironmentVariable("IGA_EVALUATION_POPULATION_TEST_DATABASE") ??
        $"Host=127.0.0.1;Port=55433;Username=iga_synthetic;Database=iga_synthetic_phase1b_pop06_author_{Guid.NewGuid().ToString("N")[..20]}";
    private static int checks;
    private static readonly AiAuthority Worker = new("synthetic-worker", AiScope.Fixed, [AiRole.Worker], [AiAction.Read, AiAction.Dispatch, AiAction.Reconcile], ["SECURITY", "OPERATIONS"]);
    private static readonly AiAuthority Consultant = new("synthetic-consultant", AiScope.Fixed, [AiRole.Consultant], [AiAction.Read], ["SECURITY", "OPERATIONS"]);
    private static readonly OutcomeAuthority OutcomeConsultant = new("synthetic-consultant", true, true, false, true, OutcomeScope.Fixed,
        [OutcomeRole.Consultant], ["SECURITY", "OPERATIONS"], [OutcomeAction.ReadOutcome, OutcomeAction.ManageOutcome], OutcomeResourceState.Mutable);
    private static void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); checks++; Console.WriteLine("PASS " + label); }
    private static async Task<T> Tx<T>(Guid run, Func<NpgsqlConnection, NpgsqlTransaction, Task<T>> action)
    {
        await using var c = new NpgsqlConnection(Connection); await c.OpenAsync(); await using var t = await c.BeginTransactionAsync();
        await SyntheticOutcomePriorityStore.AcquireRegistryFenceAsync(c, t, OutcomeScope.Fixed);
        if (run != Guid.Empty) await SyntheticRunSourceFence.AcquireAsync(c, t, "synthetic-customer", "synthetic-project", "synthetic-environment", run);
        var value = await action(c, t); await t.CommitAsync(); return value;
    }
    private static async Task<int> Main()
    {
        try { await Execute(); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static async Task Execute()
    {
        var guard = new NpgsqlConnectionStringBuilder(Connection);
        if (guard.Host != "127.0.0.1" || guard.Port != 55433 || guard.Username != "iga_synthetic" || guard.Database is null ||
            guard.Database.Length > 63 || !guard.Database.StartsWith("iga_synthetic_phase1b_pop06_author_", StringComparison.Ordinal) || !guard.Database.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
            throw new InvalidOperationException("Fresh author database guard.");
        Console.WriteLine("Population author database " + guard.Database);
        await using (var admin = new NpgsqlConnection("Host=127.0.0.1;Port=55433;Username=iga_synthetic;Database=postgres"))
        {
            await admin.OpenAsync(); await using var exists = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname=@name", admin); exists.Parameters.AddWithValue("name", guard.Database);
            if ((long)(await exists.ExecuteScalarAsync())! != 0) throw new InvalidOperationException("Fresh database name already exists; preserved without reset.");
            { await using var create = new NpgsqlCommand("CREATE DATABASE \"" + guard.Database + "\"", admin); await create.ExecuteNonQueryAsync(); }
        }
        await using (var c = new NpgsqlConnection(Connection))
        {
            await c.OpenAsync(); await using var empty = new NpgsqlCommand("SELECT count(*) FROM pg_namespace WHERE nspname LIKE 'synthetic_%'", c);
            Check((long)(await empty.ExecuteScalarAsync())! == 0, "fresh database has no owning schemas; never reset/reuse history");
        }
        var engine = new SyntheticDurableRunEngine(Connection, DemoFixtureCatalog.Scope, new(TimeSpan.FromMinutes(2), 3, 512));
        var ai = new SyntheticAiExecutionStore(Connection);
        var outcomes = new SyntheticOutcomePriorityStore(OutcomeScope.Fixed, (_, _, _, _, _) => Task.FromResult(new PlanningSourceResult(OutcomePriorityIssue.SourceUnavailable, null)));
        var adapter = new Phase1BPopulationSourceAdapter(engine, ai, outcomes);
        await using (var c = new NpgsqlConnection(Connection))
        {
            await c.OpenAsync(); await using var t = await c.BeginTransactionAsync();
            var missing = await adapter.CaptureAsync(c, t, Guid.NewGuid(), Consultant, OutcomeConsultant);
            Check(missing.Issue == Phase1BPopulationIssue.NotInitialized && missing.Population is null, "absent owning initialization closed");
        }
        await engine.InitializeAsync(); await ai.InitializeAsync();
        await using (var c = new NpgsqlConnection(Connection)) { await c.OpenAsync(); await outcomes.InitializeAsync(c); }
        var normal = await Finish(engine, ai, outcomes, false, false);
        var baseline = await Rows();
        var good = await Tx(normal, (c, t) => adapter.CaptureAsync(c, t, normal, Consultant, OutcomeConsultant));
        Check(good.Issue is null && good.Population is not null, "actual coherent owning capture succeeds");
        var capture = good.Population!;
        Check(capture.Members.Count == 2 && capture.Members.All(m => m.AffectedObjectCount == 1 && m.Occurrences.Count == 1) && capture.Gaps.Count == 0, "literal two native AI groups/objects; no deterministic members");
        Check(await Rows() == baseline, "all owning schema rows byte-equal after capture; no writes/review seeding");
        using (var doc = JsonDocument.Parse(capture.SourceCanonicalJson))
        {
            var root = doc.RootElement;
            Check(root.GetProperty("schemaVersion").GetString() == "synthetic-phase1b-evaluation-source-v1" && root.GetProperty("runState").GetString() == "Scoring", "source schema/native Scoring state");
            Check(root.GetProperty("scope").GetProperty("environmentId").GetString() == "synthetic-environment", "one exact native source environment");
            Check(capture.SourceCaptureDigest == Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(capture.SourceCanonicalJson))), "independent SHA256 entire source envelope");
            var sourceAi = JsonNode.Parse(root.GetProperty("aiSnapshotJson").GetString()!)!.AsObject();
            sourceAi["contentDigest"] = "";
            Check(AiExecutionCanonical.Digest(sourceAi) == root.GetProperty("aiSnapshotDigest").GetString(), "exact AI owning self-field-empty snapshot hash");
        }
        foreach (var occurrence in capture.Members.SelectMany(m => m.Occurrences))
        {
            var original = JsonNode.Parse(occurrence.OriginalJson)!.AsObject();
            Check(original["detectionMethod"]!.GetValue<string>() == "AI" && original["state"]!.GetValue<string>() == "Proposed" &&
                original["confidencePercent"]!.GetValue<decimal>() == 80m && original["weight"]!.GetValue<decimal>() == 1m, "unmodified native original source values");
            Check(original["category"]!.GetValue<string>() == "OPERATIONS" && original["moduleId"]!.GetValue<string>() == "SyntheticOperations", "native uppercase module/category retained alongside aliases");
            original["originalDigest"] = "";
            Check(AiExecutionCanonical.Digest(original) == occurrence.OriginalDigest, "original owning empty-field hash recipe");
            Check(occurrence.CoverageKey.InventoryId is "synthetic-ai-schedule" or "synthetic-ai-retry", "exact planned native AI key");
        }
        var deniedAi = new[] { Consultant with { Authenticated = false }, Consultant with { Active = false }, Consultant with { Revoked = true }, Consultant with { AssignmentActive = false },
            Consultant with { Roles = default }, Consultant with { Roles = [] }, Consultant with { Roles = [AiRole.Worker] }, Consultant with { Roles = [AiRole.Consultant, AiRole.Worker] }, Consultant with { Roles = [AiRole.Consultant, AiRole.Auditor] }, Consultant with { Roles = [AiRole.Auditor] },
            Consultant with { Roles = [AiRole.Reviewer] }, Consultant with { Roles = [AiRole.Consultant, AiRole.Consultant] }, Consultant with { Roles = [(AiRole)999] },
            Consultant with { Actions = default }, Consultant with { Actions = [AiAction.Override] }, Consultant with { Categories = default }, Consultant with { Categories = ["SECURITY"] },
            Consultant with { Scope = AiScope.Fixed with { EnvironmentId = "foreign" } }, Consultant with { ActorId = "other" }, Consultant with { ResourceState = AiResourceState.Published }, Consultant with { AiPolicyAllowed = false } };
        foreach (var authority in deniedAi)
        { var denied = await Tx(normal, (c, t) => adapter.CaptureAsync(c, t, normal, authority, OutcomeConsultant)); Check(denied.Issue == Phase1BPopulationIssue.Denied && denied.Population is null, "AI current authority closes " + authority.Roles.IsDefault); }
        var deniedOutcomes = new[] { OutcomeConsultant with { Authenticated = false }, OutcomeConsultant with { Active = false }, OutcomeConsultant with { Revoked = true }, OutcomeConsultant with { AssignmentActive = false },
            OutcomeConsultant with { Roles = default }, OutcomeConsultant with { Roles = [] }, OutcomeConsultant with { Roles = [OutcomeRole.CustomerOutcomeApprover] }, OutcomeConsultant with { Roles = [OutcomeRole.Consultant, OutcomeRole.CustomerOutcomeApprover] }, OutcomeConsultant with { Roles = [(OutcomeRole)999] },
            OutcomeConsultant with { Roles = [OutcomeRole.QualifiedReviewer] }, OutcomeConsultant with { Roles = [OutcomeRole.Consultant, OutcomeRole.Consultant] },
            OutcomeConsultant with { Actions = default }, OutcomeConsultant with { Categories = default }, OutcomeConsultant with { ActorId = "other" },
            OutcomeConsultant with { ResourceState = OutcomeResourceState.Published }, OutcomeConsultant with { AssignedScope = OutcomeScope.Fixed with { CustomerId = "foreign" } } };
        foreach (var authority in deniedOutcomes)
        { var denied = await Tx(normal, (c, t) => adapter.CaptureAsync(c, t, normal, Consultant, authority)); Check(denied.Issue == Phase1BPopulationIssue.Denied && denied.Population is null, "outcome current authority closes " + authority.Roles.IsDefault); }
        Check(await Rows() == baseline, "all denied reads leave owning rows unchanged");
        await using (var c = new NpgsqlConnection(Connection))
        {
            await c.OpenAsync(); await using var t = await c.BeginTransactionAsync();
            Check((await adapter.CaptureAsync(c, t, Guid.Empty, Consultant, OutcomeConsultant)).Issue == Phase1BPopulationIssue.InvalidInput, "empty run UUID denies");
            Check((await adapter.CaptureAsync(c, t, normal, null!, OutcomeConsultant)).Issue == Phase1BPopulationIssue.Denied, "null trusted authority denies");
            Check((await adapter.CaptureAsync(c, t, Guid.NewGuid(), Consultant, OutcomeConsultant)).Issue == Phase1BPopulationIssue.NotFound, "absent run closed NotFound");
            await t.RollbackAsync(); Check(c.State == System.Data.ConnectionState.Open, "caller owns rollback/disposal");
        }
        await using (var closed = new NpgsqlConnection(Connection))
        {
            var result = await adapter.CaptureAsync(closed, null!, normal, Consultant, OutcomeConsultant);
            Check(result.Issue == Phase1BPopulationIssue.InvalidInput && result.Population is null, "closed connection/no transaction denies without metadata");
        }
        await using (var c = new NpgsqlConnection(Connection))
        await using (var other = new NpgsqlConnection(Connection))
        {
            await c.OpenAsync(); await other.OpenAsync(); await using var t = await other.BeginTransactionAsync();
            var result = await adapter.CaptureAsync(c, t, normal, Consultant, OutcomeConsultant);
            Check(result.Issue == Phase1BPopulationIssue.InvalidInput && result.Population is null, "unrelated caller connection/transaction typed denial");
        }
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            try { await Tx(normal, (c, t) => adapter.CaptureAsync(c, t, normal, Consultant, OutcomeConsultant, cancelled.Token)); throw new InvalidOperationException("Cancellation swallowed."); }
            catch (OperationCanceledException) { Check(true, "caller cancellation propagates"); }
        }
        var foreignConnection = new NpgsqlConnectionStringBuilder(Connection) { Database = "iga_synthetic_phase1b_pop06_author_foreign_store" }.ConnectionString;
        var foreign = new Phase1BPopulationSourceAdapter(new SyntheticDurableRunEngine(foreignConnection, DemoFixtureCatalog.Scope, new(TimeSpan.FromMinutes(2), 3, 512)), ai, outcomes);
        var foreignResult = await Tx(normal, (c, t) => foreign.CaptureAsync(c, t, normal, Consultant, OutcomeConsultant));
        Check(foreignResult.Issue == Phase1BPopulationIssue.InvalidInput && foreignResult.Population is null, "foreign configured run store refused using same supplied transaction");
        Check(await Rows() == baseline, "input/cancellation/foreign-store cases preserve owning rows");
        var planned = await Start(engine, ai, outcomes);
        Check((await Tx(planned.RunId, (c, t) => adapter.CaptureAsync(c, t, planned.RunId, Consultant, OutcomeConsultant))).Issue == Phase1BPopulationIssue.SourceUnavailable, "incomplete Planned source unavailable");
        var gapRun = await Finish(engine, ai, outcomes, true, false);
        var gapCapture = (await Tx(gapRun, (c, t) => adapter.CaptureAsync(c, t, gapRun, Consultant, OutcomeConsultant))).Population;
        Check(gapCapture is not null && gapCapture.Members.Count == 0 && gapCapture.Gaps.Count == 2 && gapCapture.Gaps.All(g => g.State == CoverageState.InsufficientEvidence && g.ReasonCode == "AI_INSUFFICIENT_SOURCE" && g.Stage == "AI"), "actual zero findings/two terminal gaps remain outside members");
        var mixedRun = await Finish(engine, ai, outcomes, false, true);
        var mixed = (await Tx(mixedRun, (c, t) => adapter.CaptureAsync(c, t, mixedRun, Consultant, OutcomeConsultant))).Population;
        Check(mixed is not null && mixed.Members.Count == 1 && mixed.Gaps.Count == 1 && mixed.Gaps[0].State == CoverageState.NotAssessed && mixed.Gaps[0].ReasonCode == "AI_NO_VALIDATED_CONCLUSION", "actual accepted one-proposal source yields one finding/one gap");
        await using (var noReview = new NpgsqlConnection(Connection))
        {
            await noReview.OpenAsync(); await using var schema = new NpgsqlCommand("SELECT count(*) FROM pg_namespace WHERE nspname LIKE 'synthetic_%review%'", noReview);
            Check((long)(await schema.ExecuteScalarAsync())! == 0, "population does not seed assessment/evaluation review schema");
        }
        Validate(capture, 2);
        Validate(gapCapture!, 0);
        Validate(mixed!, 1);
        var emptyRun = await Finish(engine, ai, outcomes, false, false, true);
        var emptyPopulation = (await Tx(emptyRun, (c, t) => adapter.CaptureAsync(c, t, emptyRun, Consultant, OutcomeConsultant))).Population!;
        Validate(emptyPopulation, 0);
        Check(emptyPopulation.Gaps.Count == 2 && emptyPopulation.Gaps.All(g => g.ReasonCode == "AI_NO_VALIDATED_CONCLUSION"), "valid empty accepted response stays zero findings/two source gaps");
        await Concurrency(adapter, outcomes, normal);
        await RunConcurrency(adapter, engine, normal);
        await using (var c = new NpgsqlConnection(Connection))
        {
            await c.OpenAsync(); await using var update = new NpgsqlCommand("UPDATE synthetic_ai_execution.current_state SET digest=@digest WHERE run_id=@run", c);
            update.Parameters.AddWithValue("digest", new string('f', 64)); update.Parameters.AddWithValue("run", mixedRun); await update.ExecuteNonQueryAsync();
        }
        var corrupt = await Tx(mixedRun, (c, t) => adapter.CaptureAsync(c, t, mixedRun, Consultant, OutcomeConsultant));
        Check(corrupt.Issue == Phase1BPopulationIssue.IntegrityMismatch && corrupt.Population is null, "owning projection corruption denied no payload; preserved database");
        await using (var c = new NpgsqlConnection(Connection))
        {
            await c.OpenAsync(); await using var drift = new NpgsqlCommand("ALTER TABLE synthetic_ai_execution.current_state ADD COLUMN author_drift text", c); await drift.ExecuteNonQueryAsync();
        }
        var drifted = await Tx(mixedRun, (c, t) => adapter.CaptureAsync(c, t, mixedRun, Consultant, OutcomeConsultant));
        Check(drifted.Issue == Phase1BPopulationIssue.MigrationDrift && drifted.Population is null, "owning additive schema drift denied no payload; preserved database");
        Console.WriteLine($"PASS {checks} population readiness owning PostgreSQL assertions; no host/sampler/reviewer activation.");
    }
    private static void Validate(Phase1BPopulationProjection p, int memberCount)
    {
        Check(p.Members.Count == memberCount && p.VersionBindings.Count == 23 && !p.CompleteSamplingReady, "actual population count, exact inventory cardinality, incomplete readiness");
        Check(p.VersionBindings.Select(b => b.Kind).SequenceEqual(Enum.GetValues<SamplingVersionKind>()), "binding declaration order");
        Check(p.VersionBindings.Count(b => b.State == Phase1BPopulationBindingState.SourceBound) == 13 && p.MissingVersionKinds.Count == 10, "13 source-bound / 10 missing on every saved source scenario");
        var expectedMissing = new[] { SamplingVersionKind.ReassessmentBaseline, SamplingVersionKind.Collection, SamplingVersionKind.Maturity, SamplingVersionKind.AiSchema, SamplingVersionKind.AiSettings, SamplingVersionKind.Reviewer, SamplingVersionKind.ReviewerEligibility, SamplingVersionKind.Instruction, SamplingVersionKind.Conflict, SamplingVersionKind.ReviewEvents };
        Check(p.MissingVersionKinds.SequenceEqual(expectedMissing), "literal missing kind list");
        foreach (var b in p.VersionBindings)
        {
            Check(b.State == Phase1BPopulationBindingState.Missing ? b.Reference is null && b.ValueJson is null && !string.IsNullOrEmpty(b.MissingReason) : b.Reference is not null && b.ValueJson is not null && b.MissingReason is null, "exact required status/null relation " + b.Kind);
            var keys = new List<string>();
            if (b.ValueJson is not null) { using var value = JsonDocument.Parse(b.ValueJson); keys.AddRange(value.RootElement.EnumerateObject().Select(x => x.Name)); }
            if (b.KnownFactsJson is not null) { using var known = JsonDocument.Parse(b.KnownFactsJson); keys.AddRange(known.RootElement.EnumerateObject().Select(x => x.Name)); }
            Check(b.OriginPaths.SequenceEqual(keys.Distinct().Order(StringComparer.Ordinal)), "exact emitted concrete paths " + b.Kind);
        }
        Phase1BPopulationVersionBinding Binding(SamplingVersionKind kind) => p.VersionBindings.Single(b => b.Kind == kind);
        Check(Binding(SamplingVersionKind.AiProvider).ValueJson == "{\"L.$.providerVersion\":\"synthetic-fixed-provider-v1\"}" && Binding(SamplingVersionKind.AiProvider).Reference == "synthetic-ref-b4f756794ed31c6ad819e21c8027ebaf2c7086afeacbb6249cb5853935581d3f", "independent precode AiProvider bundle/reference literal");
        Check(Binding(SamplingVersionKind.AiModel).ValueJson == "{\"F.$.modelVersion\":\"synthetic-fixed-provider-v1\"}", "native declared model field provenance distinct from provider field");
        using var normalization = JsonDocument.Parse(Binding(SamplingVersionKind.Normalization).ValueJson!);
        Check(normalization.RootElement.GetProperty("P.$.CapabilityLock.NormalizationSchemaVersion").GetString() == "synthetic-normalization-v1" && normalization.RootElement.GetProperty("I[0].$.source.normalizationVersion").GetString() == "fixture-normalization-v1" && normalization.RootElement.GetProperty("I[0].$.source.redactionVersion").GetString() == "fixture-redaction-v1", "all distinct normalization origins retained");
        var schema = Binding(SamplingVersionKind.AiSchema);
        Check(schema.Reference is null && schema.ValueJson is null && schema.MissingReason == "AcceptedOutputSchemaNotSupplied" && schema.KnownFactsJson!.Contains("synthetic-ai-fixture-input-v1", StringComparison.Ordinal), "input schema/receipt digests are partial; accepted output schema absent");
        Check(Binding(SamplingVersionKind.Maturity).ValueJson is null && Binding(SamplingVersionKind.Maturity).KnownFactsJson!.Contains("maturityFixtureDigest", StringComparison.Ordinal), "maturity artifact digest never substitutes algorithm");
        Check(Binding(SamplingVersionKind.AiSettings).ValueJson is null && Binding(SamplingVersionKind.AiSettings).KnownFactsJson!.Contains("A.$.budget", StringComparison.Ordinal), "policy/budget never substitutes provider settings");
        using var plan = JsonDocument.Parse(p.RunPlanJson);
        Check(plan.RootElement.GetProperty("CapabilityLock").GetProperty("StateAtLock").GetInt32() == 1, "native default enum FixtureVerified remains numeric owning representation");
        using var source = JsonDocument.Parse(p.SourceCanonicalJson);
        Check(p.FrozenInputsJson == source.RootElement.GetProperty("frozenInputsJson").GetString(), "frozen input original exact bytes");
        Check(p.SourceCaptureDigest == Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(p.SourceCanonicalJson))) && p.ContentDigest == Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(p.CanonicalJson))), "both exact source/new envelope hashes independently calculated");
        using var envelope = JsonDocument.Parse(p.CanonicalJson);
        Check(envelope.RootElement.GetProperty("sourceObservedAtDatabaseUtc").GetString() == p.SourceObservedAtDatabaseUtc.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture) && envelope.RootElement.GetProperty("runObservedAtDatabaseUtc").GetString() == p.RunObservedAtDatabaseUtc.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture), "both observations exact seven-fraction UTC recipe");
        Check(envelope.RootElement.EnumerateObject().Count() == 17 && !envelope.RootElement.TryGetProperty("contentDigest", out _) && !envelope.RootElement.TryGetProperty("hasPopulation", out _), "exact envelope key set excludes self digest and result flags");
        foreach (var m in p.Members)
        {
            Check(m.NativeModuleId == "SyntheticOperations" && m.NativeCategoryId == "OPERATIONS" && m.NativeConfidencePercent == 80m && m.NativeConfidenceBand == "Fictional fixed confidence", "native module/category/fixed confidence lossless");
            Check(m.PrimaryModuleId == "synthetic-ref-54336c6e98655b24aaa114de36f78eec3f0795ca9e3631c90a637e27249e5e0d" && m.CategoryId == "synthetic-ref-daab2d721879873322fd815321cc0904b2ccec2cc95d2312ffe2b20a591dddf5" && m.ConfidenceBandId == "synthetic-ref-1ddedd67695857b7dbaed38cfb46248132af2fdc6d0595790bea752d24c69016", "independent literal native references");
            Check(m.Severity is SamplingSeverity.High or SamplingSeverity.Medium && m.Occurrences.All(o => o.GeneratedFindingJson.Contains("\"ConfidenceBand\":\"Fictional fixed confidence\"", StringComparison.Ordinal)), "native severity and exact generated originals");
        }
    }
    private static async Task RunConcurrency(Phase1BPopulationSourceAdapter adapter, SyntheticDurableRunEngine engine, Guid runId)
    {
        await using var c = new NpgsqlConnection(Connection); await c.OpenAsync(); await using var t = await c.BeginTransactionAsync();
        var before = await adapter.CaptureAsync(c, t, runId, Consultant, OutcomeConsultant); Check(before.HasPopulation, "run concurrency captured source with fences held");
        var writer = engine.RequestCancelAsync(DemoFixtureCatalog.Scope, runId, before.Population!.RunRevision);
        await Task.Delay(150); Check(!writer.IsCompleted, "participating owning run writer waits while population holds transaction fence");
        await t.RollbackAsync(); var changed = await writer.WaitAsync(TimeSpan.FromSeconds(15));
        Check(changed.Issue == SyntheticRunIssue.InvalidState, "run writer resumes and preserves native Scoring cancellation rejection after release");
        var after = await Tx(runId, (next, tx) => adapter.CaptureAsync(next, tx, runId, Consultant, OutcomeConsultant));
        Check(after.HasPopulation && after.Population!.RunRevision == before.Population.RunRevision && after.Population.Members.Count == 2, "later coherent source remains unchanged after rejected native cancellation");
    }
    private static async Task<string> Rows()
    {
        await using var c = new NpgsqlConnection(Connection); await c.OpenAsync();
        var names = new List<(string Schema, string Table)>();
        await using (var list = new NpgsqlCommand("SELECT schemaname,tablename FROM pg_tables WHERE schemaname LIKE 'synthetic_%' ORDER BY schemaname,tablename", c))
        { await using var reader = await list.ExecuteReaderAsync(); while (await reader.ReadAsync()) names.Add((reader.GetString(0), reader.GetString(1))); }
        var output = new StringBuilder();
        foreach (var name in names)
        {
            await using var q = new NpgsqlCommand($"SELECT COALESCE(jsonb_agg(to_jsonb(t) ORDER BY to_jsonb(t)::text),'[]'::jsonb)::text FROM \"{name.Schema}\".\"{name.Table}\" t", c);
            output.Append(name).Append(await q.ExecuteScalarAsync());
        }
        return output.ToString();
    }
    private static async Task<SyntheticRunSnapshot> Start(SyntheticDurableRunEngine engine, SyntheticAiExecutionStore ai, SyntheticOutcomePriorityStore outcomes)
    {
        var id = Guid.NewGuid();
        return await Tx(id, async (c, t) =>
        {
            var locked = await outcomes.LockOutcomesAsync(c, t, id, OutcomeConsultant, []); Check(locked.Issue is null, "owning empty approved-outcome provenance locked");
            var request = DemoPhase1BCatalog.CreateStartRequest(id.ToString("D"), DemoPhase1BAiFixture.Locks(id, locked.Lock!.ContentDigest), AiExecutionPolicy.PolicyVersion, AiExecutionPolicy.PromptVersion, AiExecutionPolicy.ProviderVersion);
            var start = await engine.StartInTransactionAsync(c, t, request, id); Check(start.Succeeded, "owning fixed run started");
            var ensured = await ai.EnsureRunAsync(c, t, Worker, DemoPhase1BAiFixture.RunLock(start.Snapshot!), DemoPhase1BAiFixture.Works(id)); Check(ensured.Succeeded, "owning frozen AI plan seeded");
            return start.Snapshot!;
        });
    }
    private static async Task<Guid> Finish(SyntheticDurableRunEngine engine, SyntheticAiExecutionStore ai, SyntheticOutcomePriorityStore outcomes, bool gap, bool mixed, bool empty = false)
    {
        var run = await Start(engine, ai, outcomes);
        run = (await engine.AcquireLeaseAsync(run.Scope, run.RunId, "synthetic-source-capture-worker")).Snapshot!;
        var generation = run.Lease!.Generation;
        run = (await engine.BeginWorkAsync(run.Scope, run.RunId, generation, run.Revision, DemoPhase1BCatalog.DeterministicPlan.ExpectedKeys)).Snapshot!;
        run = (await engine.CheckpointAsync(run.Scope, run.RunId, generation, run.Revision, DemoPhase1BCatalog.DeterministicPlan.ExpectedResults)).Snapshot!;
        run = (await engine.BeginWorkAsync(run.Scope, run.RunId, generation, run.Revision, DemoPhase1BCatalog.AiKeys)).Snapshot!;
        var work = DemoPhase1BAiFixture.Works(run.RunId).Single();
        ImmutableArray<AiUnitOutcome> results;
        if (gap)
        {
            results = await Tx(run.RunId, async (c, t) =>
            {
                var read = await ai.ReadInTransactionAsync(c, t, Worker, run.RunId);
                var terminal = await ai.RecordTerminalGapAsync(c, t, Worker, run.RunId, work.WorkId, read.Value!.Works.Single().Revision, CoverageState.InsufficientEvidence, "AI_INSUFFICIENT_SOURCE");
                Check(terminal.Succeeded, "owning terminal gap saved"); return terminal.Value;
            });
        }
        else
        {
            var attempt = await Tx(run.RunId, async (c, t) =>
            {
                var read = await ai.ReadInTransactionAsync(c, t, Worker, run.RunId);
                var reserve = await ai.ReserveAsync(c, t, Worker, run.RunId, work.WorkId, Guid.NewGuid(), read.Value!.Works.Single().Revision);
                Check(reserve.Succeeded, "owning actual reservation");
                var dispatch = await ai.MarkDispatchedAsync(c, t, Worker, run.RunId, work.WorkId, reserve.Value!.Key, reserve.Value.WorkRevision);
                Check(dispatch.Succeeded, "owning dispatched marker before pure provider"); return dispatch.Value!;
            });
            var receipt = new FakeAiProvider().Dispatch(new(attempt.Key, work.Scenario, work.PacketInputJson, work.Units, false));
            if (mixed || empty)
            {
                var output = JsonNode.Parse(receipt.OutputJson!)!.AsObject(); if (empty) output["proposals"]!.AsArray().Clear(); else output["proposals"]!.AsArray().RemoveAt(1);
                var outputJson = output.ToJsonString(); receipt = receipt with { OutputJson = outputJson, OutputDigest = AiExecutionCanonical.Hash(outputJson) };
            }
            results = await Tx(run.RunId, async (c, t) =>
            {
                var complete = await ai.CompleteAsync(c, t, Worker, run.RunId, work.WorkId, attempt.Key, receipt);
                Check(complete.Succeeded, "owning accepted source validator commits exact proposals/gaps"); return complete.Value!.Outcomes;
            });
        }
        run = (await engine.CheckpointAsync(run.Scope, run.RunId, generation, run.Revision, results.Select(SyntheticPhase1BAnalysisAdapter.Coverage).ToArray())).Snapshot!;
        var completed = await engine.CompleteCoverageAsync(run.Scope, run.RunId, generation, run.Revision); Check(completed.Succeeded, "owning twelve-unit union completes Scoring");
        return run.RunId;
    }
    private static async Task Concurrency(Phase1BPopulationSourceAdapter adapter, SyntheticOutcomePriorityStore outcomes, Guid runId)
    {
        await using var c = new NpgsqlConnection(Connection); await c.OpenAsync(); await using var t = await c.BeginTransactionAsync();
        var result = await adapter.CaptureAsync(c, t, runId, Consultant, OutcomeConsultant); Check(result.Population is not null, "capture held in caller transaction");
        var content = OutcomePriorityCanonical.Seal(new OutcomeContentVersion("capture-concurrency", 1, "SECURITY", "Fictional lock test", "Fictional traceability", OutcomeOrigin.Documented,
            [DemoPhase1BCatalog.DeterministicPlan.ExpectedKeys.First()], [], [], null, ""));
        var mutation = Tx(Guid.Empty, (next, tx) => outcomes.ApplyOutcomeAsync(next, tx, OutcomeConsultant,
            new(Guid.NewGuid(), OutcomeKind.CreateDraft, content.OutcomeId, 1, 0, content.ContentDigest, 0, null, content, "Fictional lock exclusion test")));
        await Task.Delay(150); Check(!mutation.IsCompleted, "participating registry mutation blocked by held ordered source fence");
        await t.RollbackAsync();
        var changed = await mutation.WaitAsync(TimeSpan.FromSeconds(15)); Check(changed.Issue is null, "caller release permits legitimate owning registry mutation");
        var current = await Tx(runId, (next, tx) => adapter.CaptureAsync(next, tx, runId, Consultant, OutcomeConsultant));
        Check(current.Population is not null && JsonNode.Parse(current.Population.SourceCanonicalJson)!["outcomeLockDigest"]!.GetValue<string>() == JsonNode.Parse(result.Population!.SourceCanonicalJson)!["outcomeLockDigest"]!.GetValue<string>() && current.Population.Members.Count == 2,
            "new coherent observation preserves immutable run locks despite later registry draft");
        await using var completed = await c.BeginTransactionAsync(); await completed.CommitAsync();
        var denied = await adapter.CaptureAsync(c, completed, runId, Consultant, OutcomeConsultant);
        Check(denied.Issue == Phase1BPopulationIssue.InvalidInput && denied.Population is null, "completed commit transaction typed admission denial");
        await using var rolled = await c.BeginTransactionAsync(); await rolled.RollbackAsync();
        denied = await adapter.CaptureAsync(c, rolled, runId, Consultant, OutcomeConsultant);
        Check(denied.Issue == Phase1BPopulationIssue.InvalidInput && denied.Population is null, "completed rollback transaction typed admission denial");
        var disposed = await c.BeginTransactionAsync(); await disposed.DisposeAsync();
        denied = await adapter.CaptureAsync(c, disposed, runId, Consultant, OutcomeConsultant);
        Check(denied.Issue == Phase1BPopulationIssue.InvalidInput && denied.Population is null, "disposed transaction typed admission denial");
        await using var reverse = await c.BeginTransactionAsync();
        await SyntheticRunSourceFence.AcquireAsync(c, reverse, "synthetic-customer", "synthetic-project", "synthetic-environment", runId);
        denied = await adapter.CaptureAsync(c, reverse, runId, Consultant, OutcomeConsultant);
        Check(denied.Issue == Phase1BPopulationIssue.InvalidInput && denied.Population is null, "reversed source fence order typed admission denial");
    }
}
