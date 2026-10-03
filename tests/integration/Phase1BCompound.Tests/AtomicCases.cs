using System.Collections.Immutable;
using AssessmentRuns;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using SyntheticAiExecution;
using SyntheticOutcomePriority;

internal static partial class Program
{
    private static async Task ResetOwnedFixture()
    {
        var guard = new NpgsqlConnectionStringBuilder(Connection);
        if (guard.Host != "127.0.0.1" || guard.Port != 55433 || guard.Username != "iga_synthetic" || guard.Database != "iga_synthetic_phase1b_compound_tests")
            throw new InvalidOperationException("Owned disposable fixture guard failed.");
        await using var c = new NpgsqlConnection(Connection); await c.OpenAsync();
        await using var reset = new NpgsqlCommand("""
            DROP SCHEMA IF EXISTS synthetic_task_csv CASCADE;
            DROP SCHEMA IF EXISTS synthetic_planning_tasks CASCADE;
            DROP SCHEMA IF EXISTS synthetic_fix_review CASCADE;
            DROP SCHEMA IF EXISTS synthetic_review CASCADE;
            DROP SCHEMA IF EXISTS synthetic_outcome_priority CASCADE;
            DROP SCHEMA IF EXISTS synthetic_ai_execution CASCADE;
            DROP SCHEMA IF EXISTS synthetic_assessment CASCADE;
            """, c);
        await reset.ExecuteNonQueryAsync(); Check(true, "reset only guarded owned disposable compound test schemas");
    }
    private static async Task StartRollback(DemoPhase1BService service, SyntheticDurableRunEngine engine, InjectedObserver observer, ImmutableArray<OutcomeSelection> selected)
    {
        var run = Guid.NewGuid(); observer.Target = run; observer.Operation = "start";
        await ExpectDenied(async () => await service.Start(run, selected, Token), "injected owning Start observer aborts compound transaction");
        Check((await engine.ReadAsync(DemoFixtureCatalog.Scope, run)).Issue == SyntheticRunIssue.NotFound, "failed compound Start exposes no engine run");
        Check(await Scalar("SELECT count(*) FROM synthetic_outcome_priority.run_locks WHERE run_id=@run", run) == 0, "failed compound Start rolls back immutable outcome lock");
        Check(await Scalar("SELECT count(*) FROM synthetic_assessment.outbox WHERE run_id=@run", run) == 0, "failed compound Start rolls back owning event");
        Check(await Scalar("SELECT count(*) FROM synthetic_ai_execution.run_lock WHERE run_id=@run", run) == 0, "failed compound Start leaves no AI lock");
        var accepted = await service.Start(run, selected, Token);
        Check(accepted.Succeeded && !accepted.AlreadyApplied, "same requested ID succeeds after complete rollback");
        Check((await service.Start(run, selected, Token)).AlreadyApplied, "accepted compound Start exact replay is stable");
        Check(await Scalar("SELECT count(*) FROM synthetic_outcome_priority.run_locks WHERE run_id=@run", run) == 1 &&
            await Scalar("SELECT count(*) FROM synthetic_ai_execution.run_lock WHERE run_id=@run", run) == 1, "accepted start atomically owns one outcome and AI lock");
    }
    private static async Task LateStartRollback(DemoPhase1BService service, SyntheticDurableRunEngine engine, ImmutableArray<OutcomeSelection> selected)
    {
        var observer = new EnsureFailureObserver();
        // Inject the already approved owning-store observer into this test-only service instance.
        // No host behavior, contract, authority or callback is replaced.
        var field = typeof(SyntheticAiExecutionStore).GetField("observer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        field.SetValue(service.Ai, observer);
        var run = Guid.NewGuid();
        try { await ExpectDenied(async () => await service.Start(run, selected, Token), "late owning AI ensure observer aborts actual compound Start"); }
        finally { field.SetValue(service.Ai, null); }
        Check(observer.Reached, "actual AI ensure write was reached after owning engine Start returned");
        Check((await engine.ReadAsync(DemoFixtureCatalog.Scope, run)).Issue == SyntheticRunIssue.NotFound, "late failed Start rolls back persisted owning run");
        Check(await Scalar("SELECT count(*) FROM synthetic_outcome_priority.run_locks WHERE run_id=@run", run) == 0 &&
            await Scalar("SELECT count(*) FROM synthetic_ai_execution.run_lock WHERE run_id=@run", run) == 0, "late failed Start rolls back both immutable module locks");
        Check(await Scalar("SELECT count(*) FROM synthetic_assessment.outbox WHERE run_id=@run", run) == 0 &&
            await Scalar("SELECT count(*) FROM synthetic_ai_execution.events WHERE run_id=@run", run) == 0, "late failed Start publishes no owning events");
        Check((await service.Start(run, selected, Token)).Succeeded, "same requested ID can be accepted after late all-module rollback");
    }
    private static async Task AiRollback(DemoPhase1BService service, SyntheticDurableRunEngine engine, InjectedObserver observer, ImmutableArray<OutcomeSelection> selected, bool gap)
    {
        var fixture = await Dispatch(service, engine, selected);
        var beforeAi = await Ai(service, fixture.Run.RunId); var beforeRun = await Current(engine, fixture.Run.RunId);
        var receipt = new FakeAiProvider().Dispatch(gap ? fixture.Recovery with { Scenario = AiScenario.PermanentUnknown } : fixture.Recovery);
        observer.Target = fixture.Run.RunId; observer.Operation = "checkpoint";
        await ExpectDenied(() => Commit(service, engine, fixture.Run, fixture.Generation, fixture.Recovery, receipt), gap ? "unknown gap checkpoint observer aborts" : "successful AI result checkpoint observer aborts");
        Check((await Ai(service, fixture.Run.RunId)).ContentDigest == beforeAi.ContentDigest, gap ? "gap/unknown/hold journal fully rolls back" : "accepted result/original/usage journal fully rolls back");
        var after = await Current(engine, fixture.Run.RunId);
        Check(after.Results.SequenceEqual(beforeRun.Results) && after.CheckpointSequence == beforeRun.CheckpointSequence, "aborted compound result leaves no owning coverage checkpoint");
        await Commit(service, engine, after, fixture.Generation, fixture.Recovery, receipt);
        var ai = await Ai(service, fixture.Run.RunId); var run = await Current(engine, fixture.Run.RunId);
        Check(ai.Works.Single().Outcomes.All(o => run.Results.Contains(SyntheticPhase1BAnalysisAdapter.Coverage(o))), "result/gap and exact owning coverage become durable together");
        if (!gap)
        {
            Check(ai.Works.Single().Outcomes.All(o => o.Finding?.State == "Proposed" && o.Finding.ConfidencePercent == 80m), "automatic AI originals remain Proposed with frozen confidence");
            var aiDigest = ai.ContentDigest; var sequence = run.CheckpointSequence;
            await service.Fenced(run.RunId, async (c, t) =>
            {
                var replay = await service.Ai.CompleteAsync(c, t, DemoPhase1BService.Worker, run.RunId, DemoPhase1BAiFixture.Works(run.RunId).Single().WorkId, fixture.Recovery.Attempt, receipt);
                Check(replay.Succeeded && replay.Replayed, "owning exact accepted AI receipt replay is recognized");
                var saved = await engine.CheckpointInTransactionAsync(c, t, run.Scope, run.RunId, fixture.Generation, run.Revision, replay.Value!.Outcomes.Select(SyntheticPhase1BAnalysisAdapter.Coverage).ToArray());
                Check(saved.Succeeded && saved.AlreadyApplied, "owning exact accepted checkpoint replay is recognized"); return true;
            }, Token);
            Check((await Ai(service, run.RunId)).ContentDigest == aiDigest && (await Current(engine, run.RunId)).CheckpointSequence == sequence, "completed receipt replay adds neither charge nor checkpoint");
        }
        else
        {
            Check(ai.Works.Single().Attempts.Single().Held && ai.Works.Single().Outcomes.All(o => o.State == AssessmentCoverage.CoverageState.Error), "unknown terminal gap retains hold and error coverage");
            var completed = await engine.CompleteCoverageAsync(run.Scope, run.RunId, fixture.Generation, run.Revision); Check(completed.Succeeded, "unknown gap completes union coverage without findings");
            var worker = new DemoPhase1BWorker(service, engine, NullLogger<DemoPhase1BWorker>.Instance);
            var terminal = typeof(DemoPhase1BWorker).GetMethod("TerminalRecovery", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            await (Task)terminal.Invoke(worker, [completed.Snapshot!, Token])!;
            var reconciled = await Ai(service, run.RunId);
            Check(!reconciled.Works.Single().Attempts.Single().Held && reconciled.Works.Single().Outcomes.All(o => o.State == AssessmentCoverage.CoverageState.Error), "actual terminal worker reconciles known usage without promoting unknown-gap findings");
            Check((await Current(engine, run.RunId)).Results.SequenceEqual(completed.Snapshot!.Results), "late billing leaves immutable coverage unchanged");
        }
    }
    private static async Task StaleLease(DemoPhase1BService service, SyntheticDurableRunEngine engine, InjectedObserver observer, ImmutableArray<OutcomeSelection> selected)
    {
        var fixture = await Dispatch(service, engine, selected); var before = await Ai(service, fixture.Run.RunId);
        var receipt = new FakeAiProvider().Dispatch(fixture.Recovery);
        observer.Target = fixture.Run.RunId; observer.Operation = "checkpoint"; observer.Expire = true;
        await ExpectDenied(() => Commit(service, engine, fixture.Run, fixture.Generation, fixture.Recovery, receipt), "lease expiry after owning write denies compound commit");
        observer.Expire = false;
        Check((await Ai(service, fixture.Run.RunId)).ContentDigest == before.ContentDigest, "stale lease rolls back accepted original and usage");
        Check(!(await Current(engine, fixture.Run.RunId)).Results.Any(r => DemoPhase1BCatalog.AiKeys.Contains(r.Key)), "stale lease leaves no AI owning coverage");
        var lease = await engine.AcquireLeaseAsync(fixture.Run.Scope, fixture.Run.RunId, "synthetic-compound-recovery"); Check(lease.Succeeded, "expired lease can be recovered by fresh generation");
        await Commit(service, engine, lease.Snapshot!, lease.Snapshot!.Lease!.Generation, fixture.Recovery, receipt);
        Check((await Ai(service, fixture.Run.RunId)).Works.Single().State == AiWorkState.Succeeded, "same dispatched attempt recovers under fresh lease without redispatch");
    }
}

internal sealed class EnsureFailureObserver : IAiExecutionCommitObserver
{
    internal bool Reached;
    public Task BeforeWriteAsync(string operation, Guid runId, CancellationToken ct)
    {
        if (operation == "EnsureRun") { Reached = true; throw new InjectedFailure(); }
        return Task.CompletedTask;
    }
}
