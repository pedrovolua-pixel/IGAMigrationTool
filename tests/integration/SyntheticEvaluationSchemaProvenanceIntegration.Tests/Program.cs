using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AssessmentCoverage;
using AssessmentRuns;
using DeterministicAnalysis;
using Npgsql;
using SyntheticAiExecution;
using SyntheticEvaluation;
using SyntheticEvaluationPopulationIntegration;
using SyntheticEvaluationSchemaProvenanceIntegration;
using SyntheticOutcomePriority;
using SyntheticSourceFence;

internal static class Program
{
    private static readonly string Connection = Environment.GetEnvironmentVariable("IGA_ACCEPTED_SCHEMA_TEST_DATABASE") ??
        $"Host=127.0.0.1;Port=55433;Username=iga_synthetic;Database=iga_synthetic_phase1b_schema07_author_{Guid.NewGuid().ToString("N")[..20]}";
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
        var result = await action(c, t); await t.CommitAsync(); return result;
    }
    private static async Task<int> Main()
    {
        try { await Execute(); return 0; } catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static async Task Execute()
    {
        var guard = new NpgsqlConnectionStringBuilder(Connection);
        if (guard.Host is not ("127.0.0.1" or "localhost") || guard.Port != 55433 || guard.Username != "iga_synthetic" || guard.Database is null ||
            guard.Database.Length > 63 || !guard.Database.StartsWith("iga_synthetic_phase1b_schema07_author_", StringComparison.Ordinal) || !guard.Database.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
            throw new InvalidOperationException("Fresh author database guard.");
        Console.WriteLine("Accepted-schema author database " + guard.Database);
        await using (var admin = new NpgsqlConnection("Host=127.0.0.1;Port=55433;Username=iga_synthetic;Database=postgres"))
        {
            await admin.OpenAsync(); await using var exists = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname=@name", admin); exists.Parameters.AddWithValue("name", guard.Database);
            if ((long)(await exists.ExecuteScalarAsync())! != 0) throw new InvalidOperationException("Fresh database name already exists; preserved without reset.");
            await using var create = new NpgsqlCommand("CREATE DATABASE \"" + guard.Database + "\"", admin); await create.ExecuteNonQueryAsync();
        }
        var engine = new SyntheticDurableRunEngine(Connection, DemoFixtureCatalog.Scope, new(TimeSpan.FromMinutes(2), 3, 512));
        var ai = new SyntheticAiExecutionStore(Connection);
        var outcomes = new SyntheticOutcomePriorityStore(OutcomeScope.Fixed, (_, _, _, _, _) => Task.FromResult(new PlanningSourceResult(OutcomePriorityIssue.SourceUnavailable, null)));
        var adapter = new Phase1BAcceptedSchemaSourceAdapter(engine, ai, outcomes);
        await using (var c = new NpgsqlConnection(Connection))
        {
            await c.OpenAsync(); await using var t = await c.BeginTransactionAsync();
            var missing = await adapter.CaptureAsync(c, t, Guid.NewGuid(), Consultant, OutcomeConsultant);
            Check(missing.Issue == Phase1BAcceptedSchemaIssue.NotInitialized && missing.Readiness is null, "uninitialized composed read typed closed");
            await t.RollbackAsync();
            await using var directTransaction = await c.BeginTransactionAsync();
            var direct = await ai.ReadAcceptedSchemaInTransactionAsync(c, directTransaction, Consultant, Guid.NewGuid(), new string('a', 64));
            Check(direct.Issue == AiIssue.NotInitialized && direct.Value is null, "uninitialized direct owner read typed closed");
        }
        await engine.InitializeAsync(); await ai.InitializeAsync();
        await using (var c = new NpgsqlConnection(Connection)) { await c.OpenAsync(); await outcomes.InitializeAsync(c); }
        var normal = await Finish(engine, ai, outcomes, false, false);
        var before = await Rows();
        var captured = await Tx(normal, (c, t) => adapter.CaptureAsync(c, t, normal, Consultant, OutcomeConsultant));
        Check(captured.HasReadiness && captured.Readiness is not null, "actual capture then caller COMMIT succeeds");
        Check(await Rows() == before, "whole owning table fingerprints unchanged AFTER successful caller COMMIT");
        var normalProjection = captured.Readiness!;
        await Verify(ai, normalProjection, 2, 0, true, 1);
        foreach (var variant in new[] { "mixed", "accepted-empty", "gap", "rejected", "retry", "unknown" })
        {
            var run = await Finish(engine, ai, outcomes, variant == "gap", variant == "mixed", variant == "accepted-empty", variant == "retry", variant == "rejected", variant == "unknown");
            var rows = await Rows(); var result = await Tx(run, (c, t) => adapter.CaptureAsync(c, t, run, Consultant, OutcomeConsultant));
            Check(result.HasReadiness && result.Readiness is not null, "actual terminal variant " + variant);
            var accepted = variant is "mixed" or "accepted-empty" or "retry";
            await Verify(ai, result.Readiness!, variant == "mixed" ? 1 : variant == "retry" ? 2 : 0, variant == "mixed" ? 1 : variant == "retry" ? 0 : 2, accepted, variant == "retry" ? 2 : 1);
            Check(await Rows() == rows, "committed capture keeps exact rows " + variant);
        }
        var digest = await Tx(normal, async (c, t) => (await ai.ReadInTransactionAsync(c, t, Consultant, normal)).Value!.ContentDigest);
        before = await Rows();
        var denied = new[] { Consultant with { Authenticated = false }, Consultant with { Active = false }, Consultant with { AssignmentActive = false }, Consultant with { Revoked = true },
            Consultant with { Roles = default }, Consultant with { Roles = [] }, Consultant with { Roles = [AiRole.Auditor] }, Consultant with { Roles = [AiRole.Reviewer] }, Consultant with { Roles = [AiRole.Executive] }, Consultant with { Roles = [AiRole.Support] },
            Consultant with { Roles = [AiRole.Consultant, AiRole.Worker] }, Consultant with { Roles = [AiRole.Consultant, AiRole.Consultant] }, Consultant with { Roles = [(AiRole)999] },
            Consultant with { Actions = default }, Consultant with { Actions = [] }, Consultant with { Actions = [(AiAction)999] }, Consultant with { Actions = [AiAction.Override] },
            Consultant with { Categories = default }, Consultant with { Categories = [] }, Consultant with { Categories = ["INVALID"] }, Consultant with { Categories = ["SECURITY"] },
            Consultant with { AiPolicyAllowed = false }, Consultant with { ResourceState = AiResourceState.Published }, Consultant with { Scope = AiScope.Fixed with { EnvironmentId = "foreign" } } };
        foreach (var authority in denied)
        {
            await Tx(normal, async (c, t) =>
            {
                var direct = await ai.ReadAcceptedSchemaInTransactionAsync(c, t, authority, normal, digest);
                Check(direct.Issue is AiIssue.Denied or AiIssue.WrongScope && direct.Value is null, "owner current authority deny no metadata");
                var result = await adapter.CaptureAsync(c, t, normal, authority, OutcomeConsultant);
                Check(result.Issue == Phase1BAcceptedSchemaIssue.Denied && result.Readiness is null, "composer current paired authority deny no metadata"); return true;
            });
        }
        foreach (var authority in new[] { Worker, Consultant with { Actions = [AiAction.Read, AiAction.Read, AiAction.Override], Categories = ["OPERATIONS", "OPERATIONS"] } })
        {
            var direct = await Tx(normal, (c, t) => ai.ReadAcceptedSchemaInTransactionAsync(c, t, authority, normal, digest));
            Check(direct.Succeeded && direct.Value is not null, "owner preserves existing Worker/action/category duplicate allowance");
        }
        Check((await Tx(normal, (c, t) => adapter.CaptureAsync(c, t, normal, Worker, OutcomeConsultant))).Issue == Phase1BAcceptedSchemaIssue.Denied, "worker cannot borrow composed Consultant authority");
        foreach (var authority in new[] { OutcomeConsultant with { Roles = [OutcomeRole.CustomerOutcomeApprover] }, OutcomeConsultant with { Roles = default }, OutcomeConsultant with { ActorId = "foreign" }, OutcomeConsultant with { Revoked = true } })
            Check((await Tx(normal, (c, t) => adapter.CaptureAsync(c, t, normal, Consultant, authority))).Issue == Phase1BAcceptedSchemaIssue.Denied, "no customer approver/mixed actor/outcome grant elevation");
        Check(await Rows() == before, "all authority reads preserve owning rows");
        await Inputs(ai, adapter, normal, digest);
        var planned = await Start(engine, ai, outcomes);
        var plannedRead = await Tx(planned.RunId, async (c, t) =>
        {
            var snapshot = await ai.ReadInTransactionAsync(c, t, Consultant, planned.RunId);
            return await ai.ReadAcceptedSchemaInTransactionAsync(c, t, Consultant, planned.RunId, snapshot.Value!.ContentDigest);
        });
        Check(plannedRead.Issue == AiIssue.InvalidState && plannedRead.Value is null, "owner pending source metadata unavailable");
        Check((await Tx(planned.RunId, (c, t) => adapter.CaptureAsync(c, t, planned.RunId, Consultant, OutcomeConsultant))).Issue == Phase1BAcceptedSchemaIssue.SourceUnavailable, "composed incomplete source unavailable");
        await Fence(adapter, outcomes, normal);
        await Tamper(ai, adapter, normal, digest);
        Console.WriteLine($"PASS {checks} accepted-schema PostgreSQL assertions; no activation/new source writes.");
    }
    private static async Task Verify(SyntheticAiExecutionStore ai, Phase1BAcceptedSchemaReadinessProjection p, int members, int gaps, bool accepted, int ordinal)
    {
        using var population = JsonDocument.Parse(p.PopulationCanonicalJson); var old = population.RootElement;
        Check(AiExecutionCanonical.Hash(p.CanonicalJson) == p.ContentDigest && AiExecutionCanonical.Hash(p.PopulationCanonicalJson) == p.PopulationContentDigest && AiExecutionCanonical.Hash(p.SchemaProofCanonicalJson) == p.SchemaProofContentDigest, "independent hashes every emitted envelope");
        Check(old.GetProperty("members").GetArrayLength() == members && old.GetProperty("gaps").GetArrayLength() == gaps, "actual findings/gaps cardinality retained");
        Check(old.GetProperty("missingVersionKinds").GetArrayLength() == 10 && !old.GetProperty("completeSamplingReady").GetBoolean(), "old cycle06 retains thirteen/ten all variants");
        Check(p.VersionBindings.Count == 23 && p.MissingVersionKinds.Count == (accepted ? 9 : 10) && !p.CompleteSamplingReady, "actual fourteen/nine or thirteen/ten incomplete");
        var binding = p.VersionBindings.Single(b => b.Kind == SamplingVersionKind.AiSchema);
        Check(accepted ? binding.State == Phase1BPopulationBindingState.SourceBound && binding.ValueJson is not null && binding.Reference is not null && binding.MissingReason is null :
            binding.State == Phase1BPopulationBindingState.Missing && binding.ValueJson is null && binding.Reference is null && binding.MissingReason == "AcceptedOutputSchemaNotSupplied", "exact accepted or missing schema binding");
        foreach (var prior in old.GetProperty("versionBindings").EnumerateArray())
        {
            var kind = Enum.Parse<SamplingVersionKind>(prior.GetProperty("kind").GetString()!);
            if (kind != SamplingVersionKind.AiSchema || !accepted) Check(AiExecutionCanonical.Serialize(p.VersionBindings.Single(b => b.Kind == kind)) == prior.GetRawText(), "unchanged prior binding " + kind);
        }
        await Tx(p.RunId, async (c, t) =>
        {
            var before = await ai.ReadInTransactionAsync(c, t, Consultant, p.RunId); var json = AiExecutionCanonical.Serialize(before.Value);
            var proof = await ai.ReadAcceptedSchemaInTransactionAsync(c, t, Consultant, p.RunId, before.Value!.ContentDigest);
            var after = await ai.ReadInTransactionAsync(c, t, Consultant, p.RunId);
            Check(proof.Succeeded && proof.Value is not null && proof.Value.Works.Count == 1 && proof.Value.Works.Count(w => w.Accepted is not null) == (accepted ? 1 : 0), "one accepted current successful work independent of finding count");
            Check(json == AiExecutionCanonical.Serialize(after.Value) && after.Value!.Works.SelectMany(w => w.Attempts).All(a => a.Receipt?.OutputJson is null), "old AI read exact digest/JSON/redaction compatibility");
            var work = proof.Value!.Works.Single(); Check(work.InputSchemaVersion == "synthetic-ai-fixture-input-v1", "actual saved raw input schema distinct from built packet");
            if (accepted)
            {
                var record = work.Accepted!;
                Check(record.OutputSchemaVersion == "synthetic-ai-fixture-output-v1" && record.AcceptedSnapshotSchemaVersion == "synthetic-ai-proposal-snapshot-v1" && record.Attempt.Ordinal == ordinal, "actual accepted schema triplet and last-attempt ordinal");
                await using var raw = new NpgsqlCommand("SELECT canonical,digest,source_json FROM synthetic_ai_execution.accepted_snapshot WHERE attempt_id=@id", c, t); raw.Parameters.AddWithValue("id", record.Attempt.AttemptId);
                await using var reader = await raw.ExecuteReaderAsync(); Check(await reader.ReadAsync() && AiExecutionCanonical.Hash(reader.GetString(0)) == record.AcceptedSnapshotDigest && reader.GetString(1) == record.AcceptedSnapshotDigest && AiExecutionCanonical.Hash(reader.GetString(2)) == record.OutputDigest, "independent actual raw/accepted digest recipes match metadata only");
            }
            else Check(work.Accepted is null && work.MissingReason == "NoAcceptedOutput", "no accepted output explicit absence");
            return true;
        });
    }
    private static async Task Inputs(SyntheticAiExecutionStore ai, Phase1BAcceptedSchemaSourceAdapter adapter, Guid run, string digest)
    {
        await using var c = new NpgsqlConnection(Connection); await c.OpenAsync();
        await using (var t = await c.BeginTransactionAsync())
        {
            foreach (var wrong in new[] { "", new string('A', 64), "bad" }) Check((await ai.ReadAcceptedSchemaInTransactionAsync(c, t, Consultant, run, wrong)).Issue == AiIssue.InvalidInput, "malformed expected snapshot digest");
            Check((await ai.ReadAcceptedSchemaInTransactionAsync(c, t, Consultant, Guid.Empty, digest)).Issue == AiIssue.InvalidInput, "empty owner run");
            Check((await ai.ReadAcceptedSchemaInTransactionAsync(c, t, Consultant, run, new string('a', 64))).Issue == AiIssue.SourceConflict, "stale expected owning digest source conflict");
            Check((await ai.ReadAcceptedSchemaInTransactionAsync(c, t, Consultant, Guid.NewGuid(), digest)).Issue == AiIssue.NotFound, "missing owner run");
            await t.RollbackAsync(); Check((await ai.ReadAcceptedSchemaInTransactionAsync(c, t, Consultant, run, digest)).Issue == AiIssue.InvalidInput, "rolled-back caller txn typed input denial");
        }
        await using (var t = await c.BeginTransactionAsync()) { await t.CommitAsync(); Check((await ai.ReadAcceptedSchemaInTransactionAsync(c, t, Consultant, run, digest)).Issue == AiIssue.InvalidInput, "committed caller txn typed input denial"); }
        var disposed = await c.BeginTransactionAsync(); await disposed.DisposeAsync(); Check((await ai.ReadAcceptedSchemaInTransactionAsync(c, disposed, Consultant, run, digest)).Issue == AiIssue.InvalidInput, "disposed caller txn typed input denial");
        await using (var t = await c.BeginTransactionAsync())
        {
            await SyntheticRunSourceFence.AcquireAsync(c, t, "synthetic-customer", "synthetic-project", "synthetic-environment", run);
            Check((await ai.ReadAcceptedSchemaInTransactionAsync(c, t, Consultant, run, digest)).Issue == AiIssue.InvalidInput && (await adapter.CaptureAsync(c, t, run, Consultant, OutcomeConsultant)).Issue == Phase1BAcceptedSchemaIssue.InvalidInput, "reversed source fences typed input denial");
        }
        await using (var other = new NpgsqlConnection(Connection))
        {
            await other.OpenAsync(); await using var t = await other.BeginTransactionAsync();
            Check((await ai.ReadAcceptedSchemaInTransactionAsync(c, t, Consultant, run, digest)).Issue == AiIssue.InvalidInput, "unrelated caller txn denied");
        }
        await using (var closed = new NpgsqlConnection(Connection)) Check((await ai.ReadAcceptedSchemaInTransactionAsync(closed, null!, Consultant, run, digest)).Issue == AiIssue.InvalidInput, "closed connection denied");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        try { await Tx(run, (c, t) => adapter.CaptureAsync(c, t, run, Consultant, OutcomeConsultant, cancellation.Token)); throw new InvalidOperationException("Cancellation swallowed."); }
        catch (OperationCanceledException) { Check(true, "caller cancellation propagates"); }
    }
    private static async Task Fence(Phase1BAcceptedSchemaSourceAdapter adapter, SyntheticOutcomePriorityStore outcomes, Guid run)
    {
        await using var c = new NpgsqlConnection(Connection); await c.OpenAsync(); await using var t = await c.BeginTransactionAsync();
        var result = await adapter.CaptureAsync(c, t, run, Consultant, OutcomeConsultant); Check(result.HasReadiness, "capture held in caller transaction for real writer exclusion");
        var content = OutcomePriorityCanonical.Seal(new OutcomeContentVersion("schema-fence", 1, "SECURITY", "Fictional fence", "Fictional trace", OutcomeOrigin.Documented, [DemoPhase1BCatalog.DeterministicPlan.ExpectedKeys.First()], [], [], null, ""));
        var writer = Tx(Guid.Empty, (next, tx) => outcomes.ApplyOutcomeAsync(next, tx, OutcomeConsultant, new(Guid.NewGuid(), OutcomeKind.CreateDraft, content.OutcomeId, 1, 0, content.ContentDigest, 0, null, content, "Fictional capture fence test")));
        await Task.Delay(150); Check(!writer.IsCompleted, "participating actual registry writer waits on held ordered source fences");
        await t.RollbackAsync(); Check((await writer.WaitAsync(TimeSpan.FromSeconds(15))).Issue is null, "caller release permits participating writer");
        Check(result.Readiness!.VersionBindings.Count == 23 && AiExecutionCanonical.Hash(result.Readiness.CanonicalJson) == result.Readiness.ContentDigest, "detached readiness remains exact after rollback/later writer");
    }
    private static async Task Tamper(SyntheticAiExecutionStore ai, Phase1BAcceptedSchemaSourceAdapter adapter, Guid run, string digest)
    {
        var before = await Rows();
        foreach (var mode in new[] { "missing", "raw", "normalized", "null-schema", "orphan", "current", "counter", "drift" })
        {
            await using var c = new NpgsqlConnection(Connection); await c.OpenAsync(); await using var t = await c.BeginTransactionAsync();
            await SyntheticOutcomePriorityStore.AcquireRegistryFenceAsync(c, t, OutcomeScope.Fixed); await SyntheticRunSourceFence.AcquireAsync(c, t, "synthetic-customer", "synthetic-project", "synthetic-environment", run);
            await using var update = new NpgsqlCommand(mode switch
            {
                "missing" => "ALTER TABLE synthetic_ai_execution.accepted_snapshot DISABLE TRIGGER ai_snapshot_immutable; DELETE FROM synthetic_ai_execution.accepted_snapshot WHERE attempt_id=(SELECT (canonical::jsonb->'works'->0->'attempts'->0->'key'->>'attemptId')::uuid FROM synthetic_ai_execution.current_state WHERE run_id=@run)",
                "raw" => "ALTER TABLE synthetic_ai_execution.accepted_snapshot DISABLE TRIGGER ai_snapshot_immutable; UPDATE synthetic_ai_execution.accepted_snapshot SET source_json=source_json || ' ' WHERE attempt_id=(SELECT (canonical::jsonb->'works'->0->'attempts'->0->'key'->>'attemptId')::uuid FROM synthetic_ai_execution.current_state WHERE run_id=@run)",
                "normalized" => "ALTER TABLE synthetic_ai_execution.accepted_snapshot DISABLE TRIGGER ai_snapshot_immutable; UPDATE synthetic_ai_execution.accepted_snapshot SET digest=repeat('f',64)",
                "null-schema" => "ALTER TABLE synthetic_ai_execution.accepted_snapshot DISABLE TRIGGER ai_snapshot_immutable; UPDATE synthetic_ai_execution.accepted_snapshot SET source_json=jsonb_set(source_json::jsonb,'{schemaVersion}','null'::jsonb)::text",
                "orphan" => "INSERT INTO synthetic_ai_execution.accepted_snapshot SELECT gen_random_uuid(),canonical,digest,source_json FROM synthetic_ai_execution.accepted_snapshot LIMIT 1",
                "current" => "UPDATE synthetic_ai_execution.current_state SET digest=repeat('f',64) WHERE run_id=@run",
                "counter" => "UPDATE synthetic_ai_execution.counter SET digest=repeat('f',64)",
                _ => "ALTER TABLE synthetic_ai_execution.current_state ADD COLUMN author_drift text"
            }, c, t); if (mode is "missing" or "raw" or "current") update.Parameters.AddWithValue("run", run); await update.ExecuteNonQueryAsync();
            if (mode is "missing" or "raw" or "normalized" or "null-schema")
            {
                await using var restore = new NpgsqlCommand("ALTER TABLE synthetic_ai_execution.accepted_snapshot ENABLE TRIGGER ai_snapshot_immutable", c, t);
                await restore.ExecuteNonQueryAsync();
            }
            var direct = await ai.ReadAcceptedSchemaInTransactionAsync(c, t, Consultant, run, digest); var composed = await adapter.CaptureAsync(c, t, run, Consultant, OutcomeConsultant);
            Check(direct.Issue == (mode == "drift" ? AiIssue.MigrationDrift : AiIssue.IntegrityMismatch) && direct.Value is null, "owner closed actual tamper " + mode);
            Check(composed.Issue == (mode == "drift" ? Phase1BAcceptedSchemaIssue.MigrationDrift : Phase1BAcceptedSchemaIssue.IntegrityMismatch) && composed.Readiness is null, "composer closed actual tamper " + mode);
            await t.RollbackAsync();
        }
        Check(await Rows() == before, "all intentional tamper rolled back; preserved fixture byte-identical");
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
    private static async Task<Guid> Finish(SyntheticDurableRunEngine engine, SyntheticAiExecutionStore ai, SyntheticOutcomePriorityStore outcomes, bool gap, bool mixed, bool empty = false, bool retry = false, bool rejected = false, bool unknown = false)
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
            if (retry)
            {
                await Tx(run.RunId, async (c, t) =>
                {
                    var read = await ai.ReadInTransactionAsync(c, t, Worker, run.RunId);
                    var reserve = await ai.ReserveAsync(c, t, Worker, run.RunId, work.WorkId, Guid.NewGuid(), read.Value!.Works.Single().Revision);
                    Check(reserve.Succeeded, "owning first retry reservation");
                    var dispatch = await ai.MarkDispatchedAsync(c, t, Worker, run.RunId, work.WorkId, reserve.Value!.Key, reserve.Value.WorkRevision);
                    Check(dispatch.Succeeded, "owning first retry dispatch");
                    var key = dispatch.Value!.Key;
                    var failed = new AiProviderReceipt(FakeAiProvider.ReceiptIdentity(key, AiProviderOutcome.RetryableFailure, 1, 0), key, AiProviderOutcome.RetryableFailure, 1, 0, null, null);
                    var completed = await ai.CompleteAsync(c, t, Worker, run.RunId, work.WorkId, key, failed);
                    Check(completed.Succeeded, "owning actual retryable failure receipt saved");
                    return true;
                });
            }
            var attempt = await Tx(run.RunId, async (c, t) =>
            {
                var read = await ai.ReadInTransactionAsync(c, t, Worker, run.RunId);
                var reserve = await ai.ReserveAsync(c, t, Worker, run.RunId, work.WorkId, Guid.NewGuid(), read.Value!.Works.Single().Revision);
                Check(reserve.Succeeded, "owning actual reservation");
                var dispatch = await ai.MarkDispatchedAsync(c, t, Worker, run.RunId, work.WorkId, reserve.Value!.Key, reserve.Value.WorkRevision);
                Check(dispatch.Succeeded, "owning dispatched marker before pure provider"); return dispatch.Value!;
            });
            if (unknown)
            {
                results = await Tx(run.RunId, async (c, t) =>
                {
                    var marker = await ai.MarkUnknownAsync(c, t, Worker, run.RunId, work.WorkId, attempt.Key, attempt.WorkRevision);
                    Check(marker.Succeeded, "owning actual unknown marker");
                    var read = await ai.ReadInTransactionAsync(c, t, Worker, run.RunId);
                    var failed = await ai.RecordTerminalGapAsync(c, t, Worker, run.RunId, work.WorkId, read.Value!.Works.Single().Revision, CoverageState.Error, "AI_UNKNOWN_OUTCOME");
                    Check(failed.Succeeded, "owning terminal unknown gap saved");
                    return failed.Value;
                });
            }
            else
            {
                var receipt = new FakeAiProvider().Dispatch(new(attempt.Key, work.Scenario, work.PacketInputJson, work.Units, false));
                receipt = receipt with { InputUse = 10, OutputUse = 20, ReceiptId = FakeAiProvider.ReceiptIdentity(attempt.Key, AiProviderOutcome.Response, 10, 20) };
                if (rejected)
                {
                    var invalid = "{\"schemaVersion\":null}";
                    receipt = receipt with { OutputJson = invalid, OutputDigest = AiExecutionCanonical.Hash(invalid) };
                }
                if (mixed || empty)
                {
                    var output = JsonNode.Parse(receipt.OutputJson!)!.AsObject(); if (empty) output["proposals"]!.AsArray().Clear(); else output["proposals"]!.AsArray().RemoveAt(1);
                    var outputJson = output.ToJsonString(); receipt = receipt with { OutputJson = outputJson, OutputDigest = AiExecutionCanonical.Hash(outputJson) };
                }
                results = await Tx(run.RunId, async (c, t) =>
                {
                    var complete = await ai.CompleteAsync(c, t, Worker, run.RunId, work.WorkId, attempt.Key, receipt);
                    Check(complete.Succeeded, "owning accepted/rejected source commits exact proposals/gaps"); return complete.Value!.Outcomes;
                });
            }
        }
        run = (await engine.CheckpointAsync(run.Scope, run.RunId, generation, run.Revision, results.Select(SyntheticPhase1BAnalysisAdapter.Coverage).ToArray())).Snapshot!;
        var completed = await engine.CompleteCoverageAsync(run.Scope, run.RunId, generation, run.Revision); Check(completed.Succeeded, "owning twelve-unit union completes Scoring");
        return run.RunId;
    }
}
