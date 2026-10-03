using AssessmentRuns;
using AssessmentCoverage;
using SyntheticAiExecution;

/// <summary>Pure fixed provider calls occur outside transactions. Result ledger and owning coverage checkpoint commit together.</summary>
internal sealed class DemoPhase1BWorker(DemoPhase1BService service, SyntheticDurableRunEngine engine, ILogger<DemoPhase1BWorker> logger) : BackgroundService
{
    private readonly Dictionary<Guid, Guid> owned = [];
    private readonly Dictionary<Guid, DateTimeOffset> reconciled = [];
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                foreach (var listed in await engine.ListAsync(DemoFixtureCatalog.Scope, ct))
                {
                    if (!DemoPhase1BCatalog.IsProfile(listed.ProfileCatalogId)) continue;
                    if (listed.State is not (SyntheticRunState.Planned or SyntheticRunState.Running))
                    { await TerminalRecovery(listed, ct); continue; }
                    if (listed.CancelRequested)
                    {
                        await service.Fenced(listed.RunId, async (c, t) =>
                        {
                            var ai = await service.Ai.ReadInTransactionAsync(c, t, DemoPhase1BService.Worker, listed.RunId, ct); DemoPhase1BService.Require(ai.Issue);
                            foreach (var work in ai.Value!.Works.Where(w => w.State is not (AiWorkState.Succeeded or AiWorkState.Failed or AiWorkState.Cancelled)))
                            { var cancelled = await service.Ai.CancelPendingAsync(c, t, DemoPhase1BService.Worker with { ResourceState = AiResourceState.Cancelled }, listed.RunId, work.Work.WorkId, work.Revision, ct); DemoPhase1BService.Require(cancelled.Issue); }
                            return true;
                        }, ct);
                        var final = await engine.FinalizeCancellationAsync(listed.Scope, listed.RunId, listed.Revision, owned.GetValueOrDefault(listed.RunId), ct);
                        if (final.Succeeded) owned.Remove(listed.RunId);
                        continue;
                    }
                    if (!DemoPhase1BCatalog.MatchesFrozenFixture(listed)) continue;
                    var run = listed;
                    if (!owned.TryGetValue(run.RunId, out var generation))
                    {
                        var acquired = await engine.AcquireLeaseAsync(run.Scope, run.RunId, "synthetic-phase1b-worker", null, ct);
                        if (!acquired.Succeeded) continue; run = acquired.Snapshot!; generation = run.Lease!.Generation; owned[run.RunId] = generation;
                    }
                    if (run.Lease?.Generation != generation) { owned.Remove(run.RunId); continue; }
                    var heartbeat = await engine.HeartbeatAsync(run.Scope, run.RunId, generation, ct);
                    if (!heartbeat.Succeeded) { owned.Remove(run.RunId); continue; }
                    run = heartbeat.Snapshot!;
                    var existing = run.Results.Select(r => r.Key).ToHashSet();
                    var deterministic = DemoPhase1BCatalog.DeterministicPlan.ExpectedResults.FirstOrDefault(r => !existing.Contains(r.Key));
                    if (deterministic is not null)
                    {
                        var begin = await engine.BeginWorkAsync(run.Scope, run.RunId, generation, run.Revision, [deterministic.Key], ct);
                        if (!begin.Succeeded) continue;
                        var written = await engine.CheckpointAsync(run.Scope, run.RunId, generation, begin.Snapshot!.Revision, [deterministic], ct);
                        if (!written.Succeeded) owned.Remove(run.RunId); continue;
                    }
                    var snapshot = await service.Fenced(run.RunId, async (c, t) =>
                    {
                        await Live(c, t, run.RunId, generation, ct);
                        var read = await service.Ai.ReadInTransactionAsync(c, t, DemoPhase1BService.Worker, run.RunId, ct); DemoPhase1BService.Require(read.Issue); return read.Value!;
                    }, ct);
                    var work = snapshot.Works.Single();
                    if (work.Outcomes.Length > 0)
                    {
                        var items = work.Outcomes.Select(SyntheticPhase1BAnalysisAdapter.Coverage).ToArray();
                        if (!items.All(r => existing.Contains(r.Key))) await Checkpoint(run, generation, items, ct);
                        var current = await engine.ReadAsync(run.Scope, run.RunId, ct);
                        if (current.Succeeded)
                        { var completed = await engine.CompleteCoverageAsync(run.Scope, run.RunId, generation, current.Snapshot!.Revision, ct); if (completed.Succeeded) owned.Remove(run.RunId); }
                        continue;
                    }
                    if (work.State is AiWorkState.Pending or AiWorkState.Retryable)
                    {
                        var attempt = await service.Fenced(run.RunId, async (c, t) =>
                        { await Live(c, t, run.RunId, generation, ct); return await service.Ai.ReserveAsync(c, t, DemoPhase1BService.Worker, run.RunId, work.Work.WorkId, Guid.NewGuid(), work.Revision, ct); }, ct);
                        if (attempt.Issue == AiIssue.BudgetExhausted)
                        {
                            var beginGap = await engine.BeginWorkAsync(run.Scope, run.RunId, generation, run.Revision, DemoPhase1BCatalog.AiKeys.ToArray(), ct); DemoPhase1BService.Require(beginGap.Issue);
                            await service.Fenced(run.RunId, async (c, t) =>
                            {
                                var live = await Live(c, t, run.RunId, generation, ct);
                                var r = await service.Ai.RecordTerminalGapAsync(c, t, DemoPhase1BService.Worker, run.RunId, work.Work.WorkId, work.Revision, CoverageState.NotAssessed, "AI_BUDGET_EXHAUSTED", ct); DemoPhase1BService.Require(r.Issue);
                                var saved = await engine.CheckpointInTransactionAsync(c, t, run.Scope, run.RunId, generation, live.Revision, r.Value.Select(SyntheticPhase1BAnalysisAdapter.Coverage).ToArray(), ct); DemoPhase1BService.Require(saved.Issue); return true;
                            }, ct); continue;
                        }
                        DemoPhase1BService.Require(attempt.Issue);
                        var dispatched = await service.Fenced(run.RunId, async (c, t) =>
                        { await Live(c, t, run.RunId, generation, ct); var r = await service.Ai.MarkDispatchedAsync(c, t, DemoPhase1BService.Worker, run.RunId, work.Work.WorkId, attempt.Value!.Key, attempt.Value.WorkRevision, ct); DemoPhase1BService.Require(r.Issue); return r.Value!; }, ct);
                        var recovery = new AiRecovery(dispatched.Key, work.Work.Scenario, work.Work.PacketInputJson, work.Work.Units, false);
                        // Pure fixed fixture; no network, SDK, environment or callback authority.
                        var receipt = new FakeAiProvider().Dispatch(recovery);
                        await CommitReceipt(run, generation, work.Work.WorkId, recovery, receipt, ct);
                    }
                    else if (work.State == AiWorkState.Reserved)
                    {
                        var latest = work.Attempts[^1];
                        await service.Fenced(run.RunId, async (c, t) =>
                        { await Live(c, t, run.RunId, generation, ct); var r = await service.Ai.MarkDispatchedAsync(c, t, DemoPhase1BService.Worker, run.RunId, work.Work.WorkId, latest.Key, work.Revision, ct); DemoPhase1BService.Require(r.Issue); return true; }, ct);
                    }
                    else
                    {
                        var recovery = await service.Fenced(run.RunId, async (c, t) =>
                        { await Live(c, t, run.RunId, generation, ct); var r = await service.Ai.GetRecoveryAsync(c, t, DemoPhase1BService.Worker, run.RunId, work.Work.WorkId, ct); DemoPhase1BService.Require(r.Issue); return r.Value!; }, ct);
                        var receipt = new FakeAiProvider().Lookup(recovery);
                        await CommitReceipt(run, generation, work.Work.WorkId, recovery, receipt, ct);
                    }
                }
            }
            catch (Exception error) when (error is not OperationCanceledException)
            { logger.LogWarning("Fictional Phase1B recovery waiting: {Kind}", error.GetType().Name); owned.Clear(); }
            await Task.Delay(150, ct);
        }
    }
    private async Task TerminalRecovery(SyntheticRunSnapshot run, CancellationToken ct)
    {
        if (reconciled.GetValueOrDefault(run.RunId) > DateTimeOffset.UtcNow.AddSeconds(-30)) return;
        reconciled[run.RunId] = DateTimeOffset.UtcNow;
        var authority = DemoPhase1BService.Worker with { ResourceState = run.State == SyntheticRunState.Cancelled ? AiResourceState.Cancelled : AiResourceState.Mutable };
        foreach (var fixture in DemoPhase1BAiFixture.Works(run.RunId))
        {
            var recovery = await service.Fenced(run.RunId, (c, t) => service.Ai.GetRecoveryAsync(c, t, authority, run.RunId, fixture.WorkId, ct), ct);
            if (!recovery.Succeeded || recovery.Value is null || !recovery.Value.BillingOnly) continue;
            var receipt = new FakeAiProvider().Lookup(recovery.Value);
            if (receipt.Outcome == AiProviderOutcome.Unknown) continue;
            await service.Fenced(run.RunId, async (c, t) =>
            {
                var current = await engine.ReadInTransactionAsync(c, t, run.Scope, run.RunId, ct); DemoPhase1BService.Require(current.Issue);
                if (current.Snapshot!.State is SyntheticRunState.Planned or SyntheticRunState.Running) throw new Phase1BDeniedException("SourceConflict");
                var completed = await service.Ai.CompleteAsync(c, t, authority, run.RunId, fixture.WorkId, recovery.Value.Attempt, receipt, ct); DemoPhase1BService.Require(completed.Issue);
                if (!completed.Value!.BillingOnly || !completed.Value.Outcomes.IsEmpty) throw new Phase1BDeniedException("IntegrityMismatch");
                return true;
            }, ct);
        }
    }
    private async Task<SyntheticRunSnapshot> Live(Npgsql.NpgsqlConnection c, Npgsql.NpgsqlTransaction t, Guid runId, Guid generation, CancellationToken ct)
    {
        var r = await engine.ReadInTransactionAsync(c, t, DemoFixtureCatalog.Scope, runId, ct); DemoPhase1BService.Require(r.Issue);
        if (r.Snapshot!.CancelRequested || r.Snapshot.State != SyntheticRunState.Running || r.Snapshot.Lease?.Generation != generation ||
            r.Snapshot.Lease.ExpiresAt <= r.Snapshot.ObservedAtDatabaseUtc) throw new Phase1BDeniedException("StaleLease");
        return r.Snapshot;
    }
    private async Task Checkpoint(SyntheticRunSnapshot run, Guid generation, CoverageItem[] items, CancellationToken ct)
    {
        var current = await engine.ReadAsync(run.Scope, run.RunId, ct); DemoPhase1BService.Require(current.Issue);
        var begin = await engine.BeginWorkAsync(run.Scope, run.RunId, generation, current.Snapshot!.Revision, items.Select(i => i.Key).ToArray(), ct); DemoPhase1BService.Require(begin.Issue);
        await service.Fenced(run.RunId, async (c, t) =>
        { var live = await Live(c, t, run.RunId, generation, ct); var checkpoint = await engine.CheckpointInTransactionAsync(c, t, run.Scope, run.RunId, generation, live.Revision, items, ct); DemoPhase1BService.Require(checkpoint.Issue); return true; }, ct);
    }
    private async Task CommitReceipt(SyntheticRunSnapshot run, Guid generation, string workId, AiRecovery recovery, AiProviderReceipt receipt, CancellationToken ct)
    {
        if (receipt.Outcome == AiProviderOutcome.Unknown)
        {
            var currentGap = await engine.ReadAsync(run.Scope, run.RunId, ct); DemoPhase1BService.Require(currentGap.Issue);
            var beginGap = await engine.BeginWorkAsync(run.Scope, run.RunId, generation, currentGap.Snapshot!.Revision, DemoPhase1BCatalog.AiKeys.ToArray(), ct); DemoPhase1BService.Require(beginGap.Issue);
            await service.Fenced(run.RunId, async (c, t) =>
            {
                var live = await Live(c, t, run.RunId, generation, ct);
                var read = await service.Ai.ReadInTransactionAsync(c, t, DemoPhase1BService.Worker, run.RunId, ct); DemoPhase1BService.Require(read.Issue);
                var work = read.Value!.Works.Single(w => w.Work.WorkId == workId);
                if (work.State == AiWorkState.Dispatched) { var unknown = await service.Ai.MarkUnknownAsync(c, t, DemoPhase1BService.Worker, run.RunId, workId, recovery.Attempt, work.Revision, ct); DemoPhase1BService.Require(unknown.Issue); work = work with { Revision = unknown.Value!.WorkRevision }; }
                var gap = await service.Ai.RecordTerminalGapAsync(c, t, DemoPhase1BService.Worker, run.RunId, workId, work.Revision, CoverageState.Error, "AI_UNKNOWN_OUTCOME", ct); DemoPhase1BService.Require(gap.Issue);
                var checkpoint = await engine.CheckpointInTransactionAsync(c, t, run.Scope, run.RunId, generation, live.Revision,
                    gap.Value.Select(SyntheticPhase1BAnalysisAdapter.Coverage).ToArray(), ct); DemoPhase1BService.Require(checkpoint.Issue);
                return true;
            }, ct); return;
        }
        var keys = DemoPhase1BCatalog.AiKeys;
        var current = await engine.ReadAsync(run.Scope, run.RunId, ct); DemoPhase1BService.Require(current.Issue);
        var begin = await engine.BeginWorkAsync(run.Scope, run.RunId, generation, current.Snapshot!.Revision, keys.ToArray(), ct); DemoPhase1BService.Require(begin.Issue);
        await service.Fenced(run.RunId, async (c, t) =>
        {
            var live = await Live(c, t, run.RunId, generation, ct);
            var completed = await service.Ai.CompleteAsync(c, t, DemoPhase1BService.Worker, run.RunId, workId, recovery.Attempt, receipt, ct); DemoPhase1BService.Require(completed.Issue);
            if (!completed.Value!.BillingOnly && !completed.Value.Outcomes.IsEmpty)
            {
                var checkpoint = await engine.CheckpointInTransactionAsync(c, t, run.Scope, run.RunId, generation, live.Revision,
                    completed.Value.Outcomes.Select(SyntheticPhase1BAnalysisAdapter.Coverage).ToArray(), ct); DemoPhase1BService.Require(checkpoint.Issue);
            }
            return true;
        }, ct);
    }
}
