using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AssessmentCoverage;
using AssessmentRuns;
using DeterministicAnalysis;
using FindingReview;
using Npgsql;
using SyntheticAiExecution;
using SyntheticEvaluationSourceIntegration;
using SyntheticOutcomePriority;
using SyntheticSourceFence;

internal static class Program
{
    private static readonly string Connection = Environment.GetEnvironmentVariable("IGA_EVALUATION_SOURCE_TEST_DATABASE") ??
        $"Host=127.0.0.1;Port=55433;Username=iga_synthetic;Database=iga_synthetic_phase1b_evalsource_author_{Guid.NewGuid():N}";
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
    private static async Task Main()
    {
        var guard = new NpgsqlConnectionStringBuilder(Connection);
        if (guard.Host != "127.0.0.1" || guard.Port != 55433 || guard.Username != "iga_synthetic" || guard.Database is null ||
            !guard.Database.StartsWith("iga_synthetic_phase1b_evalsource_author_", StringComparison.Ordinal) || !guard.Database.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
            throw new InvalidOperationException("Fresh author database guard.");
        Console.WriteLine("Source capture author database " + guard.Database);
        await using (var admin = new NpgsqlConnection("Host=127.0.0.1;Port=55433;Username=iga_synthetic;Database=postgres"))
        {
            await admin.OpenAsync(); await using var exists = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname=@name", admin); exists.Parameters.AddWithValue("name", guard.Database);
            if ((long)(await exists.ExecuteScalarAsync())! == 0)
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
        var adapter = new Phase1BEvaluationSourceAdapter(engine, ai, outcomes);
        await using (var c = new NpgsqlConnection(Connection))
        {
            await c.OpenAsync(); await using var t = await c.BeginTransactionAsync();
            var missing = await adapter.CaptureAsync(c, t, Guid.NewGuid(), Consultant, OutcomeConsultant);
            Check(missing.Issue == Phase1BEvaluationSourceIssue.NotInitialized && missing.Capture is null, "absent owning initialization closed");
        }
        await engine.InitializeAsync(); await ai.InitializeAsync();
        await using (var c = new NpgsqlConnection(Connection)) { await c.OpenAsync(); await outcomes.InitializeAsync(c); }
        var normal = await Finish(engine, ai, outcomes, false, false);
        var baseline = await Rows();
        var good = await Tx(normal, (c, t) => adapter.CaptureAsync(c, t, normal, Consultant, OutcomeConsultant));
        Check(good.Issue is null && good.Capture is not null, "actual coherent owning capture succeeds");
        var capture = good.Capture!;
        Check(capture.Members.Count == 2 && capture.Members.All(m => m.AffectedObjectCount == 1 && m.Occurrences.Count == 1) && capture.Gaps.Count == 0, "literal two native AI groups/objects; no deterministic members");
        Check(await Rows() == baseline, "all owning schema rows byte-equal after capture; no writes/review seeding");
        using (var doc = JsonDocument.Parse(capture.CanonicalJson))
        {
            var root = doc.RootElement;
            Check(root.GetProperty("schemaVersion").GetString() == "synthetic-phase1b-evaluation-source-v1" && root.GetProperty("runState").GetString() == "Scoring", "source schema/native Scoring state");
            Check(root.GetProperty("scope").GetProperty("environmentId").GetString() == "synthetic-environment", "one exact native source environment");
            Check(capture.ContentDigest == Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(capture.CanonicalJson))), "independent SHA256 entire source envelope");
            var sourceAi = JsonNode.Parse(root.GetProperty("aiSnapshotJson").GetString()!)!.AsObject();
            sourceAi["contentDigest"] = "";
            Check(AiExecutionCanonical.Digest(sourceAi) == capture.AiSnapshotDigest, "exact AI owning self-field-empty snapshot hash");
        }
        foreach (var occurrence in capture.Members.SelectMany(m => m.Occurrences))
        {
            var original = JsonNode.Parse(occurrence.OriginalJson)!.AsObject();
            Check(original["detectionMethod"]!.GetValue<string>() == "AI" && original["state"]!.GetValue<string>() == "Proposed" &&
                original["confidencePercent"]!.GetValue<decimal>() == 80m && original["weight"]!.GetValue<decimal>() == 1m, "unmodified native original source values");
            Check(original["category"]!.GetValue<string>() == "OPERATIONS" && original["moduleId"]!.GetValue<string>() == "SyntheticOperations", "native uppercase module/category; no aliases");
            original["originalDigest"] = "";
            Check(AiExecutionCanonical.Digest(original) == occurrence.OriginalDigest, "original owning empty-field hash recipe");
            Check(occurrence.CoverageKey.InventoryId is "synthetic-ai-schedule" or "synthetic-ai-retry", "exact planned native AI key");
        }
        var deniedAi = new[] { Consultant with { Authenticated = false }, Consultant with { Active = false }, Consultant with { Revoked = true }, Consultant with { AssignmentActive = false },
            Consultant with { Roles = default }, Consultant with { Roles = [] }, Consultant with { Roles = [AiRole.Worker] }, Consultant with { Roles = [AiRole.Auditor] },
            Consultant with { Roles = [AiRole.Reviewer] }, Consultant with { Roles = [AiRole.Consultant, AiRole.Consultant] }, Consultant with { Roles = [(AiRole)999] },
            Consultant with { Actions = default }, Consultant with { Actions = [AiAction.Override] }, Consultant with { Categories = default }, Consultant with { Categories = ["SECURITY"] },
            Consultant with { Scope = AiScope.Fixed with { EnvironmentId = "foreign" } }, Consultant with { ActorId = "other" }, Consultant with { ResourceState = AiResourceState.Published }, Consultant with { AiPolicyAllowed = false } };
        foreach (var authority in deniedAi)
        { var denied = await Tx(normal, (c, t) => adapter.CaptureAsync(c, t, normal, authority, OutcomeConsultant)); Check(denied.Issue == Phase1BEvaluationSourceIssue.Denied && denied.Capture is null, "AI current authority closes " + authority.Roles.IsDefault); }
        var deniedOutcomes = new[] { OutcomeConsultant with { Authenticated = false }, OutcomeConsultant with { Active = false }, OutcomeConsultant with { Revoked = true }, OutcomeConsultant with { AssignmentActive = false },
            OutcomeConsultant with { Roles = default }, OutcomeConsultant with { Roles = [] }, OutcomeConsultant with { Roles = [OutcomeRole.CustomerOutcomeApprover] },
            OutcomeConsultant with { Roles = [OutcomeRole.QualifiedReviewer] }, OutcomeConsultant with { Roles = [OutcomeRole.Consultant, OutcomeRole.Consultant] },
            OutcomeConsultant with { Actions = default }, OutcomeConsultant with { Categories = default }, OutcomeConsultant with { ActorId = "other" },
            OutcomeConsultant with { ResourceState = OutcomeResourceState.Published }, OutcomeConsultant with { AssignedScope = OutcomeScope.Fixed with { CustomerId = "foreign" } } };
        foreach (var authority in deniedOutcomes)
        { var denied = await Tx(normal, (c, t) => adapter.CaptureAsync(c, t, normal, Consultant, authority)); Check(denied.Issue == Phase1BEvaluationSourceIssue.Denied && denied.Capture is null, "outcome current authority closes " + authority.Roles.IsDefault); }
        Check(await Rows() == baseline, "all denied reads leave owning rows unchanged");
        await using (var c = new NpgsqlConnection(Connection))
        {
            await c.OpenAsync(); await using var t = await c.BeginTransactionAsync();
            Check((await adapter.CaptureAsync(c, t, Guid.Empty, Consultant, OutcomeConsultant)).Issue == Phase1BEvaluationSourceIssue.InvalidInput, "empty run UUID denies");
            Check((await adapter.CaptureAsync(c, t, normal, null!, OutcomeConsultant)).Issue == Phase1BEvaluationSourceIssue.Denied, "null trusted authority denies");
            Check((await adapter.CaptureAsync(c, t, Guid.NewGuid(), Consultant, OutcomeConsultant)).Issue == Phase1BEvaluationSourceIssue.NotFound, "absent run closed NotFound");
            await t.RollbackAsync(); Check(c.State == System.Data.ConnectionState.Open, "caller owns rollback/disposal");
        }
        var planned = await Start(engine, ai, outcomes);
        Check((await Tx(planned.RunId, (c, t) => adapter.CaptureAsync(c, t, planned.RunId, Consultant, OutcomeConsultant))).Issue == Phase1BEvaluationSourceIssue.SourceUnavailable, "incomplete Planned source unavailable");
        var gapRun = await Finish(engine, ai, outcomes, true, false);
        var gapCapture = (await Tx(gapRun, (c, t) => adapter.CaptureAsync(c, t, gapRun, Consultant, OutcomeConsultant))).Capture;
        Check(gapCapture is not null && gapCapture.Members.Count == 0 && gapCapture.Gaps.Count == 2 && gapCapture.Gaps.All(g => g.State == CoverageState.InsufficientEvidence && g.ReasonCode == "AI_INSUFFICIENT_SOURCE" && g.Stage == "AI"), "actual zero findings/two terminal gaps remain outside members");
        var mixedRun = await Finish(engine, ai, outcomes, false, true);
        var mixed = (await Tx(mixedRun, (c, t) => adapter.CaptureAsync(c, t, mixedRun, Consultant, OutcomeConsultant))).Capture;
        Check(mixed is not null && mixed.Members.Count == 1 && mixed.Gaps.Count == 1 && mixed.Gaps[0].State == CoverageState.NotAssessed && mixed.Gaps[0].ReasonCode == "AI_NO_VALIDATED_CONCLUSION", "actual accepted one-proposal source yields one finding/one gap");
        await Collaboration(adapter, engine, ai, outcomes, normal);
        await Concurrency(adapter, outcomes, normal);
        await using (var c = new NpgsqlConnection(Connection))
        {
            await c.OpenAsync(); await using var update = new NpgsqlCommand("UPDATE synthetic_ai_execution.current_state SET digest=@digest WHERE run_id=@run", c);
            update.Parameters.AddWithValue("digest", new string('f', 64)); update.Parameters.AddWithValue("run", mixedRun); await update.ExecuteNonQueryAsync();
        }
        var corrupt = await Tx(mixedRun, (c, t) => adapter.CaptureAsync(c, t, mixedRun, Consultant, OutcomeConsultant));
        Check(corrupt.Issue == Phase1BEvaluationSourceIssue.IntegrityMismatch && corrupt.Capture is null, "owning projection corruption denied no payload; preserved database");
        await using (var c = new NpgsqlConnection(Connection))
        {
            await c.OpenAsync(); await using var drift = new NpgsqlCommand("ALTER TABLE synthetic_ai_execution.current_state ADD COLUMN author_drift text", c); await drift.ExecuteNonQueryAsync();
        }
        var drifted = await Tx(mixedRun, (c, t) => adapter.CaptureAsync(c, t, mixedRun, Consultant, OutcomeConsultant));
        Check(drifted.Issue == Phase1BEvaluationSourceIssue.MigrationDrift && drifted.Capture is null, "owning additive schema drift denied no payload; preserved database");
        Console.WriteLine($"PASS {checks} source capture owning PostgreSQL assertions; no host/sampler/reviewer activation.");
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
    private static async Task<Guid> Finish(SyntheticDurableRunEngine engine, SyntheticAiExecutionStore ai, SyntheticOutcomePriorityStore outcomes, bool gap, bool mixed)
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
            if (mixed)
            {
                var output = JsonNode.Parse(receipt.OutputJson!)!.AsObject(); output["proposals"]!.AsArray().RemoveAt(1);
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
    private static async Task Collaboration(Phase1BEvaluationSourceAdapter adapter, SyntheticDurableRunEngine engine,
        SyntheticAiExecutionStore ai, SyntheticOutcomePriorityStore outcomes, Guid runId)
    {
        var authority = new SyntheticReviewAuthority("synthetic-consultant", true, true, false, true, SyntheticReviewScope.Fixed,
            [SyntheticReviewRole.Consultant], ["SECURITY", "OPERATIONS"], [SyntheticReviewAction.Read, SyntheticReviewAction.Review, SyntheticReviewAction.EditPresentation]);
        var reviews = new SyntheticReviewStore(Connection, SyntheticReviewScope.Fixed); await reviews.InitializeAsync();
        var seed = await Tx(runId, async (c, t) =>
        {
            var run = (await engine.ReadInTransactionAsync(c, t, DemoFixtureCatalog.Scope, runId)).Snapshot!;
            var savedAi = (await ai.ReadInTransactionAsync(c, t, Consultant, runId)).Value!;
            var locked = (await outcomes.ReadLockedAsync(c, t, runId, OutcomeConsultant)).Lock!;
            var analysis = SyntheticPhase1BAnalysisAdapter.Originals(run, savedAi, locked)!;
            return new SyntheticReviewRunSeed(SyntheticReviewScope.Fixed, runId, run.InputDigest, analysis.ContentDigest, SyntheticReviewResourceState.Mutable,
                analysis.Groups.Select(group =>
                {
                    var members = analysis.Findings.Where(f => group.OccurrenceIds.Contains(f.OccurrenceId)).ToArray();
                    return new SyntheticFindingSeed(group.RootCauseKey, members[0].CategoryId, Enum.Parse<SyntheticFindingState>(members[0].InitialDisposition.ToString()), members[0].Title,
                        members.Select(m => m.GeneratedOriginalDigest).Distinct().Order(StringComparer.Ordinal).ToImmutableArray(),
                        members.Select(m => new SyntheticOccurrenceReference(m.OccurrenceId, m.ObjectId, m.Provenance.RuleId, m.Provenance.RuleVersion, m.GeneratedOriginalDigest)).OrderBy(m => m.OccurrenceId, StringComparer.Ordinal).ToImmutableArray());
                }).OrderBy(s => s.FindingId, StringComparer.Ordinal).ToImmutableArray());
        });
        Check((await reviews.SeedAsync(seed, authority)).Succeeded, "existing assessment review seeded only by test owner");
        var before = (await Tx(runId, (c, t) => adapter.CaptureAsync(c, t, runId, Consultant, OutcomeConsultant))).Capture!;
        var finding = seed.Findings.First(f => f.InitialState == SyntheticFindingState.Proposed);
        Check((await reviews.ApplyAsync(SyntheticReviewScope.Fixed, runId, finding.FindingId, authority, new(Guid.NewGuid(), 0, SyntheticReviewEventKind.Reject, "Fictional assessment rejection"))).Succeeded,
            "existing assessment rejection recorded separately");
        Check((await reviews.ApplyAsync(SyntheticReviewScope.Fixed, runId, finding.FindingId, authority, new(Guid.NewGuid(), 1, SyntheticReviewEventKind.EditPresentation, Title: "Fictional changed display", BusinessContext: "Fictional context"))).Succeeded,
            "existing assessment presentation changed separately");
        var rows = await Rows();
        var after = (await Tx(runId, (c, t) => adapter.CaptureAsync(c, t, runId, Consultant, OutcomeConsultant))).Capture!;
        Check(before.Members.SelectMany(m => m.Occurrences).Select(o => o.OriginalJson + o.GeneratedFindingJson).SequenceEqual(after.Members.SelectMany(m => m.Occurrences).Select(o => o.OriginalJson + o.GeneratedFindingJson)),
            "assessment rejection/presentation do not rewrite captured generated originals or produce evaluation votes");
        Check(await Rows() == rows, "capture preserves all existing review rows and all other owning rows");
    }
    private static async Task Concurrency(Phase1BEvaluationSourceAdapter adapter, SyntheticOutcomePriorityStore outcomes, Guid runId)
    {
        await using var c = new NpgsqlConnection(Connection); await c.OpenAsync(); await using var t = await c.BeginTransactionAsync();
        var result = await adapter.CaptureAsync(c, t, runId, Consultant, OutcomeConsultant); Check(result.Capture is not null, "capture held in caller transaction");
        var content = OutcomePriorityCanonical.Seal(new OutcomeContentVersion("capture-concurrency", 1, "SECURITY", "Fictional lock test", "Fictional traceability", OutcomeOrigin.Documented,
            [DemoPhase1BCatalog.DeterministicPlan.ExpectedKeys.First()], [], [], null, ""));
        var mutation = Tx(Guid.Empty, (next, tx) => outcomes.ApplyOutcomeAsync(next, tx, OutcomeConsultant,
            new(Guid.NewGuid(), OutcomeKind.CreateDraft, content.OutcomeId, 1, 0, content.ContentDigest, 0, null, content, "Fictional lock exclusion test")));
        await Task.Delay(150); Check(!mutation.IsCompleted, "participating registry mutation blocked by held ordered source fence");
        await t.RollbackAsync();
        var changed = await mutation.WaitAsync(TimeSpan.FromSeconds(15)); Check(changed.Issue is null, "caller release permits legitimate owning registry mutation");
        var current = await Tx(runId, (next, tx) => adapter.CaptureAsync(next, tx, runId, Consultant, OutcomeConsultant));
        Check(current.Capture is not null && current.Capture.OutcomeLockDigest == result.Capture!.OutcomeLockDigest && current.Capture.Members.Count == 2,
            "new coherent observation preserves immutable run locks despite later registry draft");
        await using var completed = await c.BeginTransactionAsync(); await completed.CommitAsync();
        var denied = await adapter.CaptureAsync(c, completed, runId, Consultant, OutcomeConsultant);
        Check(denied.Issue == Phase1BEvaluationSourceIssue.InvalidInput && denied.Capture is null, "completed commit transaction typed admission denial");
        await using var rolled = await c.BeginTransactionAsync(); await rolled.RollbackAsync();
        denied = await adapter.CaptureAsync(c, rolled, runId, Consultant, OutcomeConsultant);
        Check(denied.Issue == Phase1BEvaluationSourceIssue.InvalidInput && denied.Capture is null, "completed rollback transaction typed admission denial");
        var disposed = await c.BeginTransactionAsync(); await disposed.DisposeAsync();
        denied = await adapter.CaptureAsync(c, disposed, runId, Consultant, OutcomeConsultant);
        Check(denied.Issue == Phase1BEvaluationSourceIssue.InvalidInput && denied.Capture is null, "disposed transaction typed admission denial");
        await using var reverse = await c.BeginTransactionAsync();
        await SyntheticRunSourceFence.AcquireAsync(c, reverse, "synthetic-customer", "synthetic-project", "synthetic-environment", runId);
        denied = await adapter.CaptureAsync(c, reverse, runId, Consultant, OutcomeConsultant);
        Check(denied.Issue == Phase1BEvaluationSourceIssue.InvalidInput && denied.Capture is null, "reversed source fence order typed admission denial");
    }
}
