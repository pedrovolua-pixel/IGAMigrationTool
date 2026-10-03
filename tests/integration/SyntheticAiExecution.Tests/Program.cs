using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using AssessmentCoverage;
using Npgsql;
using SyntheticAiExecution;
using SyntheticSourceFence;

internal static class Program
{
    private const string Database = "Host=127.0.0.1;Port=55433;Database=iga_synthetic_phase1b_ai_tests;Username=iga_synthetic";
    private static readonly SyntheticAiExecutionStore Store = new(Database);
    private static readonly FakeAiProvider Provider = new();
    private static int checks;
    private static void Check(bool value, string name) { checks++; if (!value) throw new InvalidOperationException("FAIL: " + name); }
    private static async Task<AiOperationResult<T>> Tx<T>(Func<NpgsqlConnection, NpgsqlTransaction, Task<AiOperationResult<T>>> action)
    {
        await using var c = new NpgsqlConnection(Database); await c.OpenAsync(); await using var t = await c.BeginTransactionAsync();
        var result = await action(c, t); if (result.Succeeded) await t.CommitAsync(); else await t.RollbackAsync(); return result;
    }
    private static async Task<AiExecutionSnapshot> Read(Guid run, AiAuthority? authority = null)
    { var r = await Tx((c, t) => Store.ReadInTransactionAsync(c, t, authority ?? AiFixtures.Worker, run)); Check(r.Succeeded, "coherent verified read " + r.Issue); return r.Value!; }
    private static async Task<Guid> Seed(AiScenario scenario = AiScenario.Benign, int count = 1, string? epoch = null)
    {
        var run = Guid.NewGuid(); var f = AiFixtures.Create(run, epoch ?? "fixture-epoch-" + Guid.NewGuid().ToString("N"), scenario, count);
        var r = await Tx((c, t) => Store.EnsureRunAsync(c, t, AiFixtures.Worker, f.Locked, f.Works)); Check(r.Succeeded, "fresh immutable seed " + r.Issue);
        var repeat = await Tx((c, t) => Store.EnsureRunAsync(c, t, AiFixtures.Worker, f.Locked, f.Works)); Check(repeat.Succeeded && repeat.Replayed, "same seed no write"); return run;
    }
    private static int Sum(AiExecutionSnapshot state, string prefix, bool held = false) => state.Budget.Counters.Where(x => x.Key.StartsWith(prefix, StringComparison.Ordinal)).Sum(x => held ? x.Held : x.Charged);
    private static async Task<AiAttemptSnapshot> Reserve(Guid run, string workId)
    {
        var state = await Read(run); var revision = state.Works.Single(x => x.Work.WorkId == workId).Revision; var id = Guid.NewGuid();
        var r = await Tx((c, t) => Store.ReserveAsync(c, t, AiFixtures.Worker, run, workId, id, revision)); Check(r.Succeeded, "reserve " + r.Issue);
        var replay = await Tx((c, t) => Store.ReserveAsync(c, t, AiFixtures.Worker, run, workId, id, revision)); Check(replay.Succeeded && replay.Replayed && replay.Value == r.Value, "exact reserve replay");
        var changed = await Tx((c, t) => Store.ReserveAsync(c, t, AiFixtures.Worker, run, workId, id, revision + 1)); Check(changed.Issue == AiIssue.EventConflict, "changed accepted command conflicts");
        return r.Value!;
    }
    private static async Task<AiRecovery> Dispatched(Guid run, string workId)
    {
        var attempt = await Reserve(run, workId); var r = await Tx((c, t) => Store.MarkDispatchedAsync(c, t, AiFixtures.Worker, run, workId, attempt.Key, attempt.WorkRevision));
        Check(r.Succeeded, "durable dispatch marker " + r.Issue);
        var old = await Tx((c, t) => Store.MarkDispatchedAsync(c, t, AiFixtures.Worker, run, workId, attempt.Key, attempt.WorkRevision)); Check(old.Succeeded && old.Replayed, "dispatch marker replay never authorizes new call");
        var recovery = await Tx((c, t) => Store.GetRecoveryAsync(c, t, AiFixtures.Worker, run, workId)); Check(recovery.Succeeded && !recovery.Value!.BillingOnly, "eligible outside transaction callback packet"); return recovery.Value!;
    }
    private static async Task<AiCompletion> Call(Guid run, string workId)
    {
        var dispatched = await Dispatched(run, workId); var receipt = Provider.Dispatch(dispatched);
        Check(receipt.Outcome != AiProviderOutcome.Unknown, "known scripted call");
        var complete = await Tx((c, t) => Store.CompleteAsync(c, t, AiFixtures.Worker, run, workId, dispatched.Attempt, receipt));
        Check(complete.Succeeded, "complete checkpoint-ready result " + complete.Issue);
        var replay = await Tx((c, t) => Store.CompleteAsync(c, t, AiFixtures.Worker, run, workId, dispatched.Attempt, receipt)); Check(replay.Succeeded && replay.Replayed, "usage receipt exact replay"); return complete.Value!;
    }
    private static async Task Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--crash")
        {
            var run = Guid.Parse(args[1]); var mode = args[2];
            if (mode == "reserved") await Reserve(run, "work-0"); else await Dispatched(run, "work-0");
            Console.WriteLine("READY_CRASH"); Console.Out.Flush(); await Task.Delay(Timeout.Infinite); return;
        }
        await Store.InitializeAsync(); await Store.InitializeAsync(); Check(true, "migration replay exact");
        await Budgets(); await Retries(); await UnknownAndLate(); await PriorFailureThenUnknown(); await ReceiptAndTransactionBounds(); await MetadataGaps(); await Cancellation(); await Concurrency(); await ExportsAndAuthority(); await FaultsAndCorruption(); await ImmutableProofs(); await Crashes();
        Console.WriteLine($"PASS {checks} actual PostgreSQL synthetic AI execution assertions");
    }
    private static async Task Budgets()
    {
        var run = await Seed(count: 3); await Call(run, "work-0"); var one = await Read(run);
        Check(Sum(one, "run:") == 240 && Sum(one, "run:", true) == 0, "independent300→240 release60");
        await Call(run, "work-1"); var two = await Read(run); Check(Sum(two, "run:") == 480, "two actual480");
        var failed = await Tx((c, t) => Store.ReserveAsync(c, t, AiFixtures.Worker, run, "work-2", Guid.NewGuid(), two.Works[2].Revision));
        Check(failed.Issue == AiIssue.BudgetExhausted && (await Read(run)).Works[2].Attempts.IsEmpty, "780 exceeds600 no reservation write");
        var command = new AiOverrideCommand(Guid.NewGuid(), 0, 900, 900, "OPERATIONS", "Fictional approval reason <script> is inert");
        var raised = await Tx((c, t) => Store.OverrideAsync(c, t, AiFixtures.Consultant, run, command)); Check(raised.Succeeded, "raise allowed nonterminal900 " + raised.Issue);
        var replay = await Tx((c, t) => Store.OverrideAsync(c, t, AiFixtures.Consultant, run, command)); Check(replay.Succeeded && replay.Replayed, "override historical replay");
        await Call(run, "work-2"); var three = await Read(run); Check(Sum(three, "run:") == 720 && three.Budget.RunAllowance == 900 && three.Budget.History.Single().RunTarget == 900, "third after override720");
        Check(three.Budget.Counters.Where(x => x.Key.StartsWith("period:") || x.Key.StartsWith("user:")).All(x => x.Allowance == 1200), "period/user hard ceiling unchanged");
        var terminal = await Tx((c, t) => Store.OverrideAsync(c, t, AiFixtures.Consultant, run, command with { EventId = Guid.NewGuid(), ExpectedRevision = 1, RunTarget = 1200, CategoryTarget = 1200 })); Check(terminal.Issue == AiIssue.InvalidState, "terminal override no reopening");
    }
    private static async Task Retries()
    {
        var run = await Seed(AiScenario.RetryTwice);
        for (var i = 1; i <= 3; i++)
        { var complete = await Call(run, "work-0"); Check(complete.Outcomes.Length == (i == 3 ? 2 : 0), "no partial retry finding " + i); }
        var result = await Read(run); Check(Sum(result, "run:") == 400 && result.Works[0].Attempts.Length == 3 && result.Works[0].State == AiWorkState.Succeeded, "independent80+80+240=400/three ordinals");
        foreach (var scenario in new[] { AiScenario.Empty, AiScenario.InvalidOutput, AiScenario.InvalidCitation })
        {
            var other = await Seed(scenario); var complete = await Call(other, "work-0"); var state = await Read(other);
            Check(Sum(state, "run:") == 240, "rejected/empty output charged actual");
            Check(complete.Outcomes.All(x => x.State == (scenario == AiScenario.Empty ? CoverageState.NotAssessed : CoverageState.Error) && x.Finding is null), "invalid/empty gaps exact");
        }
        var exhausted = await Seed();
        for (var ordinal = 1; ordinal <= 3; ordinal++)
        {
            var dispatched = await Dispatched(exhausted, "work-0");
            var receipt = new AiProviderReceipt(FakeAiProvider.ReceiptIdentity(dispatched.Attempt, AiProviderOutcome.RetryableFailure, 80, 0), dispatched.Attempt, AiProviderOutcome.RetryableFailure, 80, 0, null, null);
            var resultOfFailure = await Tx((c, t) => Store.CompleteAsync(c, t, AiFixtures.Worker, exhausted, "work-0", dispatched.Attempt, receipt));
            Check(resultOfFailure.Succeeded && resultOfFailure.Value!.Outcomes.Length == (ordinal == 3 ? 2 : 0), "known retryable no-result bound " + ordinal);
        }
        var exhaustedState = await Read(exhausted);
        Check(Sum(exhaustedState, "run:") == 240 && exhaustedState.Works[0].Outcomes.All(x => x.State == CoverageState.Error && x.ReasonCode == "AI_RETRY_EXHAUSTED"), "third known failure terminal no findings");
        var fourth = await Tx((c, t) => Store.ReserveAsync(c, t, AiFixtures.Worker, exhausted, "work-0", Guid.NewGuid(), exhaustedState.Works[0].Revision));
        Check(fourth.Issue == AiIssue.InvalidState && (await Read(exhausted)).Works[0].Attempts.Length == 3, "fourth dispatch denied no ordinal allocation");
    }
    private static async Task UnknownAndLate()
    {
        var run = await Seed(AiScenario.LostResponse); var dispatched = await Dispatched(run, "work-0");
        var unknown = Provider.Dispatch(dispatched); Check(unknown.Outcome == AiProviderOutcome.Unknown, "lost outcome unknown");
        var before = await Read(run); var mark = await Tx((c, t) => Store.MarkUnknownAsync(c, t, AiFixtures.Worker, run, "work-0", dispatched.Attempt, before.Works[0].Revision)); Check(mark.Succeeded, "unknown held");
        var held = await Read(run); Check(Sum(held, "run:", true) == 300 && Sum(held, "run:") == 0, "unknown keeps300");
        var bad = Provider.Lookup(dispatched) with { Attempt = dispatched.Attempt with { Ordinal = 2 } };
        var denied = await Tx((c, t) => Store.CompleteAsync(c, t, AiFixtures.Worker, run, "work-0", dispatched.Attempt, bad)); Check(denied.Issue == AiIssue.InvalidReceipt && Sum(await Read(run), "run:", true) == 300, "wrong attempt cannot release hold");
        var gap = await Tx((c, t) => Store.RecordTerminalGapAsync(c, t, AiFixtures.Worker, run, "work-0", held.Works[0].Revision, CoverageState.Error, "AI_UNKNOWN_OUTCOME")); Check(gap.Succeeded, "terminal explicit unknown gap");
        var recovery = await Tx((c, t) => Store.GetRecoveryAsync(c, t, AiFixtures.Worker with { ResourceState = AiResourceState.Deleted, AiPolicyAllowed = false }, run, "work-0"));
        Check(recovery.Succeeded && recovery.Value!.BillingOnly && recovery.Value.PacketInputJson is null && recovery.Value.Units.IsEmpty, "deleted terminal recovery identity only");
        var json = AiExecutionCanonical.Serialize(recovery.Value); Check(!json.Contains("configurationValue") && !json.Contains("packetInputJson\":\""), "recovery serialization no input");
        var receipt = Provider.Lookup(recovery.Value!); var late = await Tx((c, t) => Store.CompleteAsync(c, t, AiFixtures.Worker with { ResourceState = AiResourceState.Deleted, AiPolicyAllowed = false }, run, "work-0", dispatched.Attempt, receipt));
        Check(late.Succeeded && late.Value!.BillingOnly && late.Value.Outcomes.IsEmpty, "late billing no proposed output");
        var after = await Read(run); Check(Sum(after, "run:") == 240 && Sum(after, "run:", true) == 0 && after.Works[0].Outcomes.All(x => x.State == CoverageState.Error), "late use reconciles, gap immutable");
        var activeRun = await Seed(AiScenario.LostResponse); var active = await Dispatched(activeRun, "work-0");
        var restored = await Tx((c, t) => Store.CompleteAsync(c, t, AiFixtures.Worker, activeRun, "work-0", active.Attempt, Provider.Lookup(active))); Check(restored.Succeeded && restored.Value!.Outcomes.Length == 2, "known lookup active commits original once");
        var replayDeleted = await Tx((c, t) => Store.CompleteAsync(c, t, AiFixtures.Worker with { ResourceState = AiResourceState.Deleted }, activeRun, "work-0", active.Attempt, Provider.Lookup(active)));
        Check(replayDeleted.Succeeded && replayDeleted.Value!.BillingOnly && replayDeleted.Value.Outcomes.IsEmpty, "accepted replay after deletion hides original payload");
    }
    private static async Task Cancellation()
    {
        var before = await Seed(); var reserved = await Reserve(before, "work-0");
        var cancelled = await Tx((c, t) => Store.CancelPendingAsync(c, t, AiFixtures.Worker, before, "work-0", reserved.WorkRevision)); Check(cancelled.Succeeded, "predispatch cancel");
        var state = await Read(before); Check(Sum(state, "run:", true) == 0 && Sum(state, "run:") == 0 && state.Works[0].State == AiWorkState.Cancelled, "known undispatched zero-use release");
        var after = await Seed(AiScenario.LostResponse); var dispatched = await Dispatched(after, "work-0"); var current = await Read(after);
        await Tx((c, t) => Store.CancelPendingAsync(c, t, AiFixtures.Worker, after, "work-0", current.Works[0].Revision));
        Check(Sum(await Read(after), "run:", true) == 300, "postdispatch cancellation keeps hold");
        var recovery = await Tx((c, t) => Store.GetRecoveryAsync(c, t, AiFixtures.Worker, after, "work-0")); Check(recovery.Value!.BillingOnly && recovery.Value.PacketInputJson is null, "cancelled lookup metadata");
        var complete = await Tx((c, t) => Store.CompleteAsync(c, t, AiFixtures.Worker, after, "work-0", dispatched.Attempt, Provider.Lookup(recovery.Value))); Check(complete.Succeeded && complete.Value!.BillingOnly, "cancelled billing only");
    }
    private static async Task PriorFailureThenUnknown()
    {
        var run = await Seed(AiScenario.RetryThenLostResponse); var first = await Dispatched(run, "work-0"); var firstReceipt = Provider.Dispatch(first);
        var failed = await Tx((c, t) => Store.CompleteAsync(c, t, AiFixtures.Worker, run, "work-0", first.Attempt, firstReceipt));
        Check(failed.Succeeded && firstReceipt.InputUse == 80 && firstReceipt.OutputUse == 0, "A1 known failure80");
        var second = await Dispatched(run, "work-0"); Check(second.Attempt.LogicalKey == first.Attempt.LogicalKey && second.Attempt.Ordinal == 2 && second.Attempt.AttemptId != first.Attempt.AttemptId, "same logical key exact distinct A2");
        Check(Provider.Dispatch(second).Outcome == AiProviderOutcome.Unknown, "fixed A2 lost response"); var current = await Read(run);
        var marked = await Tx((c, t) => Store.MarkUnknownAsync(c, t, AiFixtures.Worker, run, "work-0", second.Attempt, current.Works[0].Revision)); Check(marked.Succeeded, "A2 durable unknown");
        var unknown = await Read(run); Check(Sum(unknown, "run:") == 80 && Sum(unknown, "run:", true) == 300, "independent A1actual80+A2held300=380");
        var wrong = await Tx((c, t) => Store.CompleteAsync(c, t, AiFixtures.Worker, run, "work-0", second.Attempt, firstReceipt));
        Check(wrong.Issue == AiIssue.InvalidReceipt && (await Read(run)).ContentDigest == unknown.ContentDigest, "A1 receipt cannot satisfy exact A2 or claim UUID");
        var gap = await Tx((c, t) => Store.RecordTerminalGapAsync(c, t, AiFixtures.Worker, run, "work-0", unknown.Works[0].Revision, CoverageState.Error, "AI_UNKNOWN_OUTCOME")); Check(gap.Succeeded, "A2 terminal error retains hold");
        var recovery = await Tx((c, t) => Store.GetRecoveryAsync(c, t, AiFixtures.Worker, run, "work-0")); Check(recovery.Value!.Attempt == second.Attempt && recovery.Value.BillingOnly && recovery.Value.PacketInputJson is null, "terminal lookup selects A2 metadata");
        var late = await Tx((c, t) => Store.CompleteAsync(c, t, AiFixtures.Worker, run, "work-0", second.Attempt, Provider.Lookup(recovery.Value)));
        Check(late.Succeeded && late.Value!.BillingOnly && late.Value.Outcomes.IsEmpty, "A2 late billing-only");
        var final = await Read(run); Check(Sum(final, "run:") == 320 && Sum(final, "run:", true) == 0 && final.Works[0].Outcomes.All(x => x.State == CoverageState.Error && x.Finding is null), "independent80+240=320 immutable gap");
    }
    private static async Task ReceiptAndTransactionBounds()
    {
        var run = await Seed(); var recovery = await Dispatched(run, "work-0"); var receipt = Provider.Dispatch(recovery); var before = await Read(run);
        var wrongOrder = await Tx(async (c, t) =>
        {
            await SyntheticRunSourceFence.AcquireAsync(c, t, AiScope.Fixed.CustomerId, AiScope.Fixed.ProjectId, AiScope.Fixed.EnvironmentId, run);
            return await Store.ReadInTransactionAsync(c, t, AiFixtures.Worker, run);
        });
        Check(wrongOrder.Issue == AiIssue.InvalidInput && wrongOrder.Value is null, "run fence before registry rejected without acquiring registry");
        foreach (var (input, output) in new[] { (101, 160), (80, 201), (-1, 160), (80, -1) })
        {
            var invalid = receipt with { InputUse = input, OutputUse = output, ReceiptId = FakeAiProvider.ReceiptIdentity(recovery.Attempt, receipt.Outcome, input, output) };
            var denied = await Tx((c, t) => Store.CompleteAsync(c, t, AiFixtures.Worker, run, "work-0", recovery.Attempt, invalid));
            Check(denied.Issue == AiIssue.InvalidReceipt && (await Read(run)).ContentDigest == before.ContentDigest, "component bound denies without use release or event claim");
        }
        await using (var c = new NpgsqlConnection(Database))
        {
            await c.OpenAsync(); await using var t = await c.BeginTransactionAsync();
            var complete = await Store.CompleteAsync(c, t, AiFixtures.Worker, run, "work-0", recovery.Attempt, receipt);
            Check(complete.Succeeded && complete.Value!.Outcomes.Length == 2, "response arrived and complete written before owning checkpoint");
            await t.RollbackAsync();
        }
        Check((await Read(run)).ContentDigest == before.ContentDigest, "caller checkpoint fault rolls back originals charge counters event together");
        var valid = await Tx((c, t) => Store.CompleteAsync(c, t, AiFixtures.Worker, run, "work-0", recovery.Attempt, receipt)); Check(valid.Succeeded, "rolled back receipt is reusable");
        var changed = receipt with { OutputJson = receipt.OutputJson + " ", OutputDigest = AiExecutionCanonical.Hash(receipt.OutputJson + " ") };
        var conflict = await Tx((c, t) => Store.CompleteAsync(c, t, AiFixtures.Worker, run, "work-0", recovery.Attempt, changed)); Check(conflict.Issue == AiIssue.EventConflict, "accepted receipt altered payload conflicts");
        var pending = await Seed(); var reserved = await Reserve(pending, "work-0");
        var revoked = await Tx((c, t) => Store.MarkDispatchedAsync(c, t, AiFixtures.Worker with { Revoked = true }, pending, "work-0", reserved.Key, reserved.WorkRevision));
        Check(revoked.Issue == AiIssue.Denied && (await Read(pending)).Works[0].State == AiWorkState.Reserved, "revocation before marker denies callback authorization");
        var equality = await Seed(); var atLimit = await Dispatched(equality, "work-0"); var normal = Provider.Dispatch(atLimit);
        var maximum = normal with { InputUse = 100, OutputUse = 200, ReceiptId = FakeAiProvider.ReceiptIdentity(atLimit.Attempt, normal.Outcome, 100, 200) };
        var accepted = await Tx((c, t) => Store.CompleteAsync(c, t, AiFixtures.Worker, equality, "work-0", atLimit.Attempt, maximum));
        Check(accepted.Succeeded && Sum(await Read(equality), "run:") == 300 && Sum(await Read(equality), "run:", true) == 0, "component equality100+200 accepted actual300");
    }
    private static async Task Concurrency()
    {
        var run = await Seed(count: 3); var start = new TaskCompletionSource();
        async Task<AiOperationResult<AiAttemptSnapshot>> Race(int i) { await start.Task; return await Tx((c, t) => Store.ReserveAsync(c, t, AiFixtures.Worker, run, "work-" + i, Guid.NewGuid(), 1)); }
        var tasks = new[] { Race(0), Race(1), Race(2) }; start.SetResult(); var results = await Task.WhenAll(tasks);
        Check(results.Count(x => x.Succeeded) == 2 && results.Count(x => x.Issue == AiIssue.BudgetExhausted) == 1, "three concurrent reservations exactlytwo");
        Check(Sum(await Read(run), "run:", true) == 600, "equality600 accepted no overspend");
        var epoch = "shared-epoch-" + Guid.NewGuid().ToString("N"); var runs = new[] { await Seed(count: 2, epoch: epoch), await Seed(count: 2, epoch: epoch), await Seed(epoch: epoch) };
        foreach (var id in runs[..2]) { await Reserve(id, "work-0"); await Reserve(id, "work-1"); }
        var denied = await Tx((c, t) => Store.ReserveAsync(c, t, AiFixtures.Worker, runs[2], "work-0", Guid.NewGuid(), 1)); Check(denied.Issue == AiIssue.BudgetExhausted, "shared epoch1200 caps fifth reservation");
    }
    private static async Task MetadataGaps()
    {
        foreach (var (reason, expected) in new[] { ("AI_DISABLED", CoverageState.Excluded), ("AI_EXCLUDED_SOURCE", CoverageState.Excluded), ("AI_INSUFFICIENT_SOURCE", CoverageState.InsufficientEvidence), ("AI_CONFLICTING_SOURCE", CoverageState.InsufficientEvidence), ("AI_UNSUPPORTED_SOURCE", CoverageState.Unsupported), ("AI_INACCESSIBLE_SOURCE", CoverageState.Inaccessible), ("AI_REDACTED_SOURCE", CoverageState.Redacted) })
        {
            var run = await Seed(); var disabled = reason is "AI_DISABLED" or "AI_EXCLUDED_SOURCE";
            var authority = AiFixtures.Worker with { AiPolicyAllowed = !disabled };
            var result = await Tx((c, t) => Store.RecordTerminalGapAsync(c, t, authority, run, "work-0", 1, expected, reason));
            Check(result.Succeeded && result.Value.All(x => x.State == expected && x.ReasonCode == reason && x.Stage == "AI" && x.Finding is null), "exact authorized metadata gap " + reason);
            Check(Sum(await Read(run), "run:") == 0 && (await Read(run)).Works[0].Attempts.IsEmpty, "metadata gap no use/reservation/dispatch");
            Check(!AiExecutionCanonical.Serialize(result.Value).Contains("configurationValue"), "metadata gap no packet serialization");
        }
        var fresh = await Seed(); var before = await Read(fresh);
        foreach (var (reason, state) in new[] { ("AI_BUDGET_EXHAUSTED", CoverageState.Redacted), ("AI_UNKNOWN_OUTCOME", CoverageState.Error), ("AI_DISABLED", CoverageState.NotAssessed), ("AI_BUDGET_EXHAUSTED", CoverageState.NotAssessed) })
        {
            var invalid = await Tx((c, t) => Store.RecordTerminalGapAsync(c, t, AiFixtures.Worker, fresh, "work-0", 1, state, reason));
            Check(invalid.Issue == AiIssue.InvalidState && (await Read(fresh)).ContentDigest == before.ContentDigest, "wrong responsible gap condition or state rejected");
        }
    }
    private static async Task ExportsAndAuthority()
    {
        var run = await Seed(); await Call(run, "work-0");
        var export = new AiExportAuthority("synthetic-auditor", AiScope.Fixed, [AiRole.Auditor], ["SECURITY", "OPERATIONS"], true, true, true, false, true, true, true, AiResourceState.Mutable);
        var verified = await Tx((c, t) => Store.VerifyForExportInTransactionAsync(c, t, export, run)); Check(verified.Succeeded && verified.Value!.Units.Length == 2, "genuine auditor export proof");
        var json = AiExecutionCanonical.Serialize(verified.Value); Check(!json.Contains("configurationValue") && !json.Contains("proposalCanonicalJson") && !json.Contains("Fictional AI schedule"), "export proof excludes source text");
        var noGrant = await Tx((c, t) => Store.VerifyForExportInTransactionAsync(c, t, export with { AuditorScopedGrant = false }, run)); Check(noGrant.Issue == AiIssue.Denied, "Auditor grant required");
        foreach (var authority in new[] { AiFixtures.Worker with { Revoked = true }, AiFixtures.Worker with { AssignmentActive = false }, AiFixtures.Worker with { Categories = ["SECURITY"] }, AiFixtures.Worker with { ResourceState = AiResourceState.Deleted } })
        { var read = await Tx((c, t) => Store.ReadInTransactionAsync(c, t, authority, run)); Check(!read.Succeeded && read.Value is null, "denied read no payload"); }
    }
    private sealed class Fault : IAiExecutionCommitObserver
    { public Task BeforeWriteAsync(string operation, Guid runId, CancellationToken ct) => throw new InvalidOperationException("owned fault"); }
    private static async Task FaultsAndCorruption()
    {
        var run = await Seed(); var faultStore = new SyntheticAiExecutionStore(Database, new Fault());
        var before = await Read(run); var fail = await Tx((c, t) => faultStore.ReserveAsync(c, t, AiFixtures.Worker, run, "work-0", Guid.NewGuid(), 1)); Check(!fail.Succeeded, "injected write failure");
        Check((await Read(run)).ContentDigest == before.ContentDigest, "fault caller rollback preserves all stores");
        await using var c = new NpgsqlConnection(Database); await c.OpenAsync();
        await using var corrupt = new NpgsqlCommand("UPDATE synthetic_ai_execution.current_state SET digest='corrupt' WHERE run_id=@run", c); corrupt.Parameters.AddWithValue("run", run); await corrupt.ExecuteNonQueryAsync();
        try { var read = await Tx((cx, tx) => Store.ReadInTransactionAsync(cx, tx, AiFixtures.Worker, run)); Check(read.Issue == AiIssue.IntegrityMismatch && read.Value is null, "corrupt proof denies whole projection"); }
        finally { await using var restore = new NpgsqlCommand("UPDATE synthetic_ai_execution.current_state SET digest=(SELECT after_digest FROM synthetic_ai_execution.events WHERE run_id=@run ORDER BY revision DESC LIMIT 1) WHERE run_id=@run", c); restore.Parameters.AddWithValue("run", run); await restore.ExecuteNonQueryAsync(); }
        Check((await Read(run)).ContentDigest == before.ContentDigest, "owned corruption bytes restored");
        await using (var drift = new NpgsqlCommand("ALTER TABLE synthetic_ai_execution.current_state ADD COLUMN owned_drift text", c)) await drift.ExecuteNonQueryAsync();
        try { var read = await Tx((cx, tx) => Store.ReadInTransactionAsync(cx, tx, AiFixtures.Worker, run)); Check(read.Issue == AiIssue.MigrationDrift && read.Value is null, "physical schema drift denies source projection"); }
        finally { await using var restore = new NpgsqlCommand("ALTER TABLE synthetic_ai_execution.current_state DROP COLUMN owned_drift", c); await restore.ExecuteNonQueryAsync(); }
        Check((await Read(run)).ContentDigest == before.ContentDigest, "owned schema drift restored exactly");
        await using var mutation = new NpgsqlCommand("UPDATE synthetic_ai_execution.events SET kind='tamper' WHERE run_id=@run", c); mutation.Parameters.AddWithValue("run", run);
        try { await mutation.ExecuteNonQueryAsync(); Check(false, "history update unexpectedly allowed"); } catch (PostgresException) { Check(true, "append-only trigger enforced"); }
    }
    private static async Task Crashes()
    {
        foreach (var mode in new[] { "reserved", "dispatched" })
        {
            var run = await Seed(AiScenario.LostResponse); var executable = Environment.ProcessPath ?? throw new InvalidOperationException("runtime path missing");
            var info = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            info.ArgumentList.Add(typeof(Program).Assembly.Location); info.ArgumentList.Add("--crash"); info.ArgumentList.Add(run.ToString("D")); info.ArgumentList.Add(mode);
            using var child = Process.Start(info) ?? throw new InvalidOperationException("owned child failed");
            var ready = false;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            while (await child.StandardOutput.ReadLineAsync(timeout.Token) is { } line) if (line == "READY_CRASH") { ready = true; break; }
            Check(ready, "child committed crash stage"); child.Kill(); await child.WaitForExitAsync();
            var state = await Read(run); Check(Sum(state, "run:", true) == 300 && state.Works[0].Attempts.Length == 1, "actual hard kill keeps reservation/attempt");
            var attempt = state.Works[0].Attempts[0];
            if (mode == "reserved")
            {
                var mark = await Tx((c, t) => Store.MarkDispatchedAsync(c, t, AiFixtures.Worker, run, "work-0", attempt.Key, state.Works[0].Revision)); Check(mark.Succeeded, "recovery reuses reserved attempt");
            }
            var recovery = await Tx((c, t) => Store.GetRecoveryAsync(c, t, AiFixtures.Worker, run, "work-0"));
            var complete = await Tx((c, t) => Store.CompleteAsync(c, t, AiFixtures.Worker, run, "work-0", recovery.Value!.Attempt, Provider.Lookup(recovery.Value)));
            Check(complete.Succeeded && Sum(await Read(run), "run:") == 240 && (await Read(run)).Works[0].Attempts.Length == 1, "restart exact lookup no new attempt or charge");
        }
    }
    private static async Task ImmutableProofs()
    {
        var run = await Seed(); await Call(run, "work-0"); var before = await Read(run);
        await using var c = new NpgsqlConnection(Database); await c.OpenAsync();
        foreach (var target in new[] { "receipt", "snapshot" })
        {
            await using var t = await c.BeginTransactionAsync();
            await SyntheticRunSourceFence.AcquireAsync(c, t, AiScope.Fixed.CustomerId, AiScope.Fixed.ProjectId, AiScope.Fixed.EnvironmentId, SyntheticAiExecutionStore.RegistryFenceId);
            var sql = target == "receipt" ? "ALTER TABLE synthetic_ai_execution.events DISABLE TRIGGER ai_event_immutable; UPDATE synthetic_ai_execution.events SET receipt_canonical='true' WHERE run_id=@run AND kind='Complete'; ALTER TABLE synthetic_ai_execution.events ENABLE TRIGGER ai_event_immutable" :
                "ALTER TABLE synthetic_ai_execution.accepted_snapshot DISABLE TRIGGER ai_snapshot_immutable; UPDATE synthetic_ai_execution.accepted_snapshot SET canonical='{}' WHERE attempt_id IN (SELECT (j->'key'->>'attemptId')::uuid FROM synthetic_ai_execution.current_state s CROSS JOIN LATERAL jsonb_array_elements(s.canonical::jsonb->'works') w CROSS JOIN LATERAL jsonb_array_elements(w->'attempts') j WHERE s.run_id=@run); ALTER TABLE synthetic_ai_execution.accepted_snapshot ENABLE TRIGGER ai_snapshot_immutable";
            await using (var corrupt = new NpgsqlCommand(sql, c, t)) { corrupt.Parameters.AddWithValue("run", run); await corrupt.ExecuteNonQueryAsync(); }
            var denied = await Store.ReadInTransactionAsync(c, t, AiFixtures.Worker, run); Check(denied.Issue == AiIssue.IntegrityMismatch && denied.Value is null, "parse-valid " + target + " only corruption denies whole source");
            var export = new AiExportAuthority("synthetic-auditor", AiScope.Fixed, [AiRole.Auditor], ["OPERATIONS"], true, true, true, false, true, true, true, AiResourceState.Mutable);
            var exportDenied = await Store.VerifyForExportInTransactionAsync(c, t, export, run); Check(exportDenied.Issue == AiIssue.IntegrityMismatch && exportDenied.Value is null, "corrupt " + target + " denies Auditor metadata too");
            await t.RollbackAsync();
        }
        Check((await Read(run)).ContentDigest == before.ContentDigest, "bounded proof corruption restored by transaction rollback");
        var empty = await Seed(AiScenario.Empty); await Call(empty, "work-0");
        await using var count = new NpgsqlCommand("SELECT count(*) FROM synthetic_ai_execution.accepted_snapshot p JOIN synthetic_ai_execution.current_state s ON s.canonical::jsonb #>> '{works,0,attempts,0,key,attemptId}'=p.attempt_id::text WHERE s.run_id=@run AND p.canonical::jsonb->'proposals'='[]'::jsonb", c);
        count.Parameters.AddWithValue("run", empty); Check((long)(await count.ExecuteScalarAsync() ?? 0L) == 1, "valid empty success full validator snapshot remains durable");
        var wrongDb = new SyntheticAiExecutionStore(Database.Replace("iga_synthetic_phase1b_ai_tests", "iga_synthetic_phase1b_other", StringComparison.Ordinal));
        var wrong = await Tx((cx, tx) => wrongDb.ReadInTransactionAsync(cx, tx, AiFixtures.Worker, run)); Check(wrong.Issue == AiIssue.WrongScope && wrong.Value is null, "supplied transaction must match exact configured database");
    }
}
