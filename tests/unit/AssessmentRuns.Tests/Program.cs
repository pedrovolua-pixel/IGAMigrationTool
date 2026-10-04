using AssessmentCoverage;
using AssessmentOrchestration;
using AssessmentRuns;

var passed = 0;
var scope = DemoFixtureCatalog.Scope;
const string Connection = "Host=127.0.0.1;Port=55433;Database=iga_synthetic_d3;Username=iga_synthetic";
var policy = new SyntheticRunPolicy(TimeSpan.FromSeconds(2), 2, 512);
var engine = new SyntheticDurableRunEngine(Connection, scope, policy);
var request = DemoFixtureCatalog.CreateStartRequest("baseline-complete", "profile-standard", "synthetic-start-v1");
var wrong = scope with { CustomerId = "synthetic-other-customer" };

Check("SYN-D3-001 fixed catalog explicit outcome digest", () =>
{
    Require(DemoFixtureCatalog.Baselines.Count == 9 && DemoFixtureCatalog.Profiles.Count == 11 && DemoFixtureCatalog.Profiles.Count(item => !DemoPlanningTaskCatalog.IsProfile(item.Id)) == 10);
    Require(DemoFixtureCatalog.Profiles.Select(item => item.Id).Take(4).SequenceEqual(new[]
        { "profile-standard", "profile-comparison", "synthetic-analysis-equal-v1", "synthetic-analysis-operations-v1" }));
    Require(DemoFixtureCatalog.Baselines.Where(item => !DemoAnalysisCatalog.IsAnalysisBaseline(item.Id)).Select(item => item.Id)
        .SequenceEqual(new[] { "baseline-complete", "baseline-gaps", "baseline-recovery", "baseline-not-applicable", "baseline-ai-configuration-v1" }));
    Require(DemoFixtureCatalog.Profiles.Where(item => !DemoAnalysisCatalog.IsAnalysisProfile(item.Id)).Select(item => item.Id)
        .SequenceEqual(new[] { "profile-standard", "profile-comparison", "profile-ai-preview-v1", "profile-ai-preview-empty-v1" }));
    Require(request.Versions.ScriptedResultsDigest == DemoFixtureCatalog.ScriptDigest("baseline-complete"));
    var script = DemoFixtureCatalog.Baselines[0].ScriptedResults;
    Require(DemoFixtureCatalog.ComputeScriptedResultsDigest(script) == DemoFixtureCatalog.ComputeScriptedResultsDigest(script.Reverse()));
    Require(script.Count == 3 && script[1].State == CoverageState.Finding);
});
Check("SYN-D3-002 recovery and zero applicable fixtures", () =>
{
    Require(DemoFixtureCatalog.Baselines.Single(item => item.Id == "baseline-recovery").ScriptedResults.Count == 40);
    var none = DemoFixtureCatalog.Baselines.Single(item => item.Id == "baseline-not-applicable");
    var plan = SyntheticBaselineInventoryPlanner.Plan(none.Capability, none.Inventory, scope).Plan!;
    Require(ExecutableCoverageProjector.Project(plan.ExpectedKeys, plan.DeclaredItems).Measure == new ExecutableCoverageMeasure(0, 0));
});
Check("SYN-D3-003 remote connection refused before opening", () => Throws<ArgumentException>(() =>
    new SyntheticDurableRunEngine("Host=example.invalid;Database=iga_synthetic_d3;Username=synthetic", scope, policy)));
Check("SYN-D3-004 unrelated local database refused", () => Throws<ArgumentException>(() =>
    new SyntheticDurableRunEngine("Host=127.0.0.1;Database=production;Username=synthetic", scope, policy)));
Check("SYN-D3-005 unknown catalog refused", () => Throws<ArgumentException>(() =>
    DemoFixtureCatalog.CreateStartRequest("unknown", "profile-standard", "key")));
Check("SYN-D3-006 invalid lease policy refused", () => Throws<ArgumentException>(() =>
    new SyntheticDurableRunEngine(Connection, scope, policy with { LeaseDuration = TimeSpan.Zero })));
Check("SYN-D3-007 invalid retry policy refused", () => Throws<ArgumentException>(() =>
    new SyntheticDurableRunEngine(Connection, scope, policy with { MaxAttempts = 0 })));
await CheckAsync("SYN-D3-008 start wrong scope before DB", async () => Issue(await engine.StartAsync(request with { Scope = wrong }), SyntheticRunIssue.WrongScope));
await CheckAsync("SYN-D3-009 read wrong scope before DB", async () => Issue(await engine.ReadAsync(wrong, Guid.NewGuid()), SyntheticRunIssue.WrongScope));
await CheckAsync("SYN-D3-010 lease wrong scope before DB", async () => Issue(await engine.AcquireLeaseAsync(wrong, Guid.NewGuid(), "worker"), SyntheticRunIssue.WrongScope));
await CheckAsync("SYN-D3-011 checkpoint wrong scope before DB", async () => Issue(await engine.CheckpointAsync(wrong, Guid.NewGuid(), Guid.NewGuid(), 1, []), SyntheticRunIssue.WrongScope));
await CheckAsync("SYN-D3-012 cancel wrong scope before DB", async () => Issue(await engine.RequestCancelAsync(wrong, Guid.NewGuid(), 1), SyntheticRunIssue.WrongScope));
await CheckAsync("SYN-D3-013 version shape before DB", async () => Issue(await engine.StartAsync(request with
{ Versions = request.Versions with { ScriptedResultsDigest = "unknown" } }), SyntheticRunIssue.InvalidInput));
await CheckAsync("SYN-D3-014 invalid plan before DB", async () => Issue(await engine.StartAsync(request with
{ Baseline = request.Baseline with { Objects = [] } }), SyntheticRunIssue.InvalidPlan));

Console.WriteLine($"{passed} synthetic durable run pure cases passed; these do not verify persistence.");
if (!args.Contains("--postgres", StringComparer.Ordinal)) return;
await engine.InitializeAsync();
await engine.InitializeAsync();
var dbPassed = 0;
var dbKey = "synthetic-d3-" + Guid.NewGuid().ToString("N");
request = request with { IdempotencyKey = dbKey };
var original = Snapshot(await engine.StartAsync(request));
var replay = await engine.StartAsync(request);
Require(replay.AlreadyApplied && replay.Snapshot!.RunId == original.RunId);
dbPassed++;
Issue(await engine.StartAsync(request with { Versions = request.Versions with { ProfileVersion = "different-version" } }), SyntheticRunIssue.IdempotencyConflict);
dbPassed++;
var acquired = Snapshot(await engine.AcquireLeaseAsync(scope, original.RunId, "synthetic-worker-a", original.Revision));
var generation = acquired.Lease!.Generation;
Issue(await engine.AcquireLeaseAsync(scope, original.RunId, "synthetic-worker-b"), SyntheticRunIssue.LeaseUnavailable);
dbPassed++;
var scriptResults = DemoFixtureCatalog.Baselines[0].ScriptedResults;
Issue(await engine.CompleteCoverageAsync(scope, original.RunId, generation, acquired.Revision), SyntheticRunIssue.InvalidCoverage);
dbPassed++;
var checkpoint = Snapshot(await engine.CheckpointAsync(scope, original.RunId, generation, acquired.Revision, [scriptResults[0]]));
Require(checkpoint.CheckpointSequence == 1 && checkpoint.Results.Count == 2 && checkpoint.Attempts.Single().Outcome == SyntheticAttemptOutcome.Succeeded);
dbPassed++;
Require((await engine.CheckpointAsync(scope, original.RunId, generation, acquired.Revision, [scriptResults[0]])).AlreadyApplied);
Issue(await engine.CheckpointAsync(scope, original.RunId, generation, checkpoint.Revision,
    [scriptResults[0] with { State = CoverageState.Finding }]), SyntheticRunIssue.ResultConflict);
dbPassed++;
Issue(await engine.CheckpointAsync(scope, original.RunId, generation, acquired.Revision, [scriptResults[1]]), SyntheticRunIssue.RevisionConflict);
dbPassed++;
var failed = Snapshot(await engine.RecordAttemptFailureAsync(scope, original.RunId, generation, checkpoint.Revision, scriptResults[1].Key, "synthetic-timeout"));
Require(failed.Results.Count == 2 && failed.Attempts.Count == 2 && failed.Attempts[^1].Outcome == SyntheticAttemptOutcome.Failed);
dbPassed++;
var recovered = Snapshot(await engine.CheckpointAsync(scope, original.RunId, generation, failed.Revision, [scriptResults[1], scriptResults[2]]));
Require(recovered.Attempts.Count == 4 && recovered.Attempts.Single(item => item.Key == scriptResults[1].Key && item.Outcome == SyntheticAttemptOutcome.Succeeded).AttemptNumber == 2);
dbPassed++;
var completed = Snapshot(await engine.CompleteCoverageAsync(scope, original.RunId, generation, recovered.Revision));
Require(completed.State == SyntheticRunState.Scoring && completed.ScoringPaused && completed.CoverageSummary!.Kind == CoverageCompletionKind.Complete);
Require(completed.CoverageSummary!.ExecutableCoverage == new ExecutableCoverageMeasure(3, 3));
Require(completed.FrozenInputs == request.Versions && completed.Lease is null);
Require(((IList<CoverageKey>)completed.Plan.ExpectedKeys).IsReadOnly && ((IList<CoverageItem>)completed.Results).IsReadOnly);
dbPassed++;
var restarted = new SyntheticDurableRunEngine(Connection, scope, policy);
await restarted.InitializeAsync();
var afterRestart = Snapshot(await restarted.ReadAsync(scope, completed.RunId));
Require(afterRestart.InputDigest == completed.InputDigest && afterRestart.Revision == completed.Revision && afterRestart.Results.SequenceEqual(completed.Results));
dbPassed++;
var events = await engine.ReadOutboxAsync(scope, completed.RunId);
Require(events.Count >= 6 && events.All(item => item.SchemaVersion == SyntheticDurableRunEngine.WorkSchemaVersion));
var delivery = await engine.DeliverOutboxAsync(scope, events[0].EventId, "synthetic-unit-consumer");
Require(delivery.Issue is null && !delivery.AlreadyApplied);
Require((await engine.DeliverOutboxAsync(scope, events[0].EventId, "synthetic-unit-consumer")).AlreadyApplied);
dbPassed++;

var cancelRun = Snapshot(await engine.StartAsync(request with { IdempotencyKey = dbKey + "-cancel" }));
var active = Snapshot(await engine.AcquireLeaseAsync(scope, cancelRun.RunId, "synthetic-cancel-worker"));
var cancelGeneration = active.Lease!.Generation;
var begun = Snapshot(await engine.BeginWorkAsync(scope, active.RunId, cancelGeneration, active.Revision, [scriptResults[0].Key]));
var cancelled = Snapshot(await engine.RequestCancelAsync(scope, active.RunId, begun.Revision));
Issue(await engine.BeginWorkAsync(scope, active.RunId, cancelGeneration, cancelled.Revision, [scriptResults[1].Key]), SyntheticRunIssue.CancellationRequested);
Issue(await engine.CheckpointAsync(scope, active.RunId, cancelGeneration, cancelled.Revision, [scriptResults[1]]), SyntheticRunIssue.CancellationRequested);
Issue(await engine.FinalizeCancellationAsync(scope, active.RunId, cancelled.Revision), SyntheticRunIssue.LeaseUnavailable);
Issue(await engine.HeartbeatAsync(scope, active.RunId, cancelGeneration), SyntheticRunIssue.CancellationRequested);
dbPassed++;
var inflight = Snapshot(await engine.CheckpointAsync(scope, active.RunId, cancelGeneration, cancelled.Revision, [scriptResults[0]]));
var final = Snapshot(await engine.FinalizeCancellationAsync(scope, active.RunId, inflight.Revision, cancelGeneration));
Require(final.State == SyntheticRunState.Cancelled && final.Results.Count == 2 && final.CancelRequested);
Issue(await engine.AcquireLeaseAsync(scope, active.RunId, "synthetic-worker-never-resume"), SyntheticRunIssue.CancellationRequested);
dbPassed++;

var exhaustRun = Snapshot(await engine.StartAsync(request with { IdempotencyKey = dbKey + "-exhaust" }));
var exhaustLease = Snapshot(await engine.AcquireLeaseAsync(scope, exhaustRun.RunId, "synthetic-error-worker"));
var errorGeneration = exhaustLease.Lease!.Generation;
var firstFailure = Snapshot(await engine.RecordAttemptFailureAsync(scope, exhaustRun.RunId, errorGeneration, exhaustLease.Revision, scriptResults[0].Key, "synthetic-rule-timeout"));
var exhausted = Snapshot(await engine.RecordAttemptFailureAsync(scope, exhaustRun.RunId, errorGeneration, firstFailure.Revision, scriptResults[0].Key, "synthetic-rule-timeout"));
Require(exhausted.Results.Single(item => item.Key == scriptResults[0].Key) == new CoverageItem(scriptResults[0].Key, CoverageState.Error, "synthetic-rule-timeout", "synthetic-worker"));
Require(exhausted.Attempts.Count == 2 && exhausted.Attempts[^1].Outcome == SyntheticAttemptOutcome.Exhausted);
Issue(await engine.CheckpointAsync(scope, exhaustRun.RunId, errorGeneration, exhausted.Revision, [scriptResults[0]]), SyntheticRunIssue.ResultConflict);
dbPassed++;

var leaseRun = Snapshot(await engine.StartAsync(request with { IdempotencyKey = dbKey + "-lease" }));
var leaseEngine = new SyntheticDurableRunEngine(Connection, scope, policy with { LeaseDuration = TimeSpan.FromMilliseconds(300) });
var oldLease = Snapshot(await leaseEngine.AcquireLeaseAsync(scope, leaseRun.RunId, "synthetic-expired-worker"));
await Task.Delay(400);
var newLease = Snapshot(await engine.AcquireLeaseAsync(scope, leaseRun.RunId, "synthetic-replacement-worker"));
Require(oldLease.Lease!.Generation != newLease.Lease!.Generation);
Issue(await engine.CheckpointAsync(scope, leaseRun.RunId, oldLease.Lease.Generation, newLease.Revision, [scriptResults[0]]), SyntheticRunIssue.StaleLease);
dbPassed++;

var rollbackRun = Snapshot(await engine.StartAsync(request with { IdempotencyKey = dbKey + "-rollback" }));
var rollbackLease = Snapshot(await engine.AcquireLeaseAsync(scope, rollbackRun.RunId, "synthetic-rollback-worker"));
var rollbackEngine = new SyntheticDurableRunEngine(Connection, scope, policy, new FailCheckpoint());
var beforeRollbackEvents = await engine.ReadOutboxAsync(scope, rollbackRun.RunId);
await ThrowsAsync<InjectedFailure>(() => rollbackEngine.CheckpointAsync(scope, rollbackRun.RunId, rollbackLease.Lease!.Generation, rollbackLease.Revision, [scriptResults[0]]));
var unchanged = Snapshot(await engine.ReadAsync(scope, rollbackRun.RunId));
Require(unchanged.Revision == rollbackLease.Revision && unchanged.CheckpointSequence == 0 && unchanged.Results.Count == 1 && unchanged.Attempts.Count == 0);
Require((await engine.ReadOutboxAsync(scope, rollbackRun.RunId)).Count == beforeRollbackEvents.Count);
dbPassed++;
Require((await engine.ListAsync(scope)).Any(item => item.RunId == original.RunId));
Require(afterRestart.ObservedAtDatabaseUtc >= afterRestart.CreatedAt);
dbPassed++;
Console.WriteLine($"{dbPassed} actual PostgreSQL durable run groups passed; synthetic only, no full assessment completion.");

void Check(string id, Action action) { try { action(); passed++; } catch (Exception exception) { throw new Exception(id, exception); } }
async Task CheckAsync(string id, Func<Task> action) { try { await action(); passed++; } catch (Exception exception) { throw new Exception(id, exception); } }
static SyntheticRunSnapshot Snapshot(SyntheticRunCommandResult result)
{
    if (!result.Succeeded) throw new Exception($"Synthetic command refused: {result.Issue}");
    return result.Snapshot!;
}
static void Issue(SyntheticRunCommandResult result, SyntheticRunIssue expected) => Require(result.Issue == expected && result.Snapshot is null);
static void Require(bool condition) { if (!condition) throw new Exception("Unexpected synthetic durable run result."); }
static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected typed refusal."); }
static async Task ThrowsAsync<T>(Func<Task> action) where T : Exception { try { await action(); } catch (T) { return; } throw new Exception("Expected typed failure."); }
internal sealed class InjectedFailure : Exception;
internal sealed class FailCheckpoint : ISyntheticRunCommitObserver
{
    public Task BeforeCommitAsync(string operation, Guid runId, CancellationToken cancellationToken) =>
        operation == "checkpoint" ? Task.FromException(new InjectedFailure()) : Task.CompletedTask;
}
