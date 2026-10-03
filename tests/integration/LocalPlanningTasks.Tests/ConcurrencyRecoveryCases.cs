using Npgsql;
using SyntheticFixReview;
using SyntheticPlanningTasks;

internal static class ConcurrencyRecoveryCases
{
    private sealed class Hold : ISyntheticPlanningTaskCommitObserver
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task BeforeCommitAsync(string operation, Guid run, string task, Guid @event, CancellationToken ct)
        { Entered.TrySetResult(); await Released.Task.WaitAsync(ct); }
    }
    private sealed class Fail : ISyntheticPlanningTaskCommitObserver
    {
        public Task BeforeCommitAsync(string operation, Guid run, string task, Guid @event, CancellationToken ct) => throw new OperationCanceledException("controlled_owned_v14_commit_failure");
    }
    internal static async Task Run()
    {
        var fixture = new DomainFixture(); await fixture.Initialize(); var initial = await fixture.Read();
        var target = initial.Options[0]; await fixture.Review(target); var ready = await fixture.Read(); target = DatabaseCases.Option(ready, target.Identity.TaskId);
        var first = fixture.Command(ready, target); var second = fixture.Command(ready, target);
        var concurrent = await Task.WhenAll(fixture.Apply(target, first), fixture.Apply(target, second));
        Check.Equal(concurrent.Count(r => r.Receipt is not null && !r.AlreadyApplied), 1, "actual-concurrent-conversions-one-creation");
        Check.Equal(concurrent.Count(r => r.AlreadyExistsTaskId == target.Identity.TaskId), 1, "actual-fresh-concurrent-conversion-no-write-AlreadyExists-loser");
        var current = await fixture.Read(); var entry = DatabaseCases.Entry(current, target.Identity.TaskId);
        Check.Equal(entry.History.Length, 1, "concurrent-conversions-one-continuous-event");
        var commandA = fixture.Command(current, DatabaseCases.Option(current, target.Identity.TaskId), PlanningTaskKind.Comment, 1);
        var commandB = commandA with { EventId = Guid.NewGuid(), Reason = "Fictional distinct concurrent comment" };
        var revisions = await Task.WhenAll(fixture.Apply(target, commandA), fixture.Apply(target, commandB));
        Check.Equal(revisions.Count(r => r.Succeeded), 1, "same-expected-task-revision-one-winner");
        Check.Equal(revisions.Count(r => r.Issue == PlanningTaskIssue.RevisionConflict), 1, "same-expected-task-revision-one-conflict");
        var exact = revisions[0].Succeeded ? commandA : commandB;
        var before = await OwnedDatabase.FullRows(fixture.RunId);
        var replays = await Task.WhenAll(fixture.Apply(target, exact), fixture.Apply(target, exact));
        Check.That(replays.All(r => r.AlreadyApplied) && replays[0].Receipt == replays[1].Receipt, "concurrent-exact-replay-same-original-receipt");
        Check.Equal(await OwnedDatabase.FullRows(fixture.RunId), before, "concurrent-exact-replay-complete-rows-invariant");
        await ArtifactWriterFence(fixture, target.Identity.TaskId);
        await Rollback();
        Check.Group("TC14-T04/T05/T06 actual concurrency/source fence/commit failure/cancellation/reconnect");
    }
    private static async Task ArtifactWriterFence(DomainFixture fixture, string task)
    {
        var current = await fixture.Read(); var target = DatabaseCases.Option(current, task); var entry = DatabaseCases.Entry(current, task);
        var command = fixture.Command(current, target, PlanningTaskKind.Comment, entry.Revision);
        var hold = new Hold(); var pending = fixture.Store(hold).ApplyAsync(fixture.RunId, task, Policies.Consultant, command);
        Task<ArtifactReviewApplyResult>? writer = null;
        try
        {
            await hold.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var source = fixture.ArtifactSource().Source!;
            var tagged = new NpgsqlConnectionStringBuilder(OwnedDatabase.Connection) { ApplicationName = "v14-owned-artifact-writer" }.ConnectionString;
            var store = new SyntheticFixReviewStore(tagged, ArtifactReviewScope.Fixed, fixture.ReadArtifact);
            var attestation = target.CurrentAttestations[0];
            writer = store.ApplyAsync(fixture.RunId, attestation.ArtifactId, Policies.ArtifactConsultant,
                new(Guid.NewGuid(), ArtifactReviewKind.WithdrawReview, attestation.Revision, source.Binding.SourceDigest, "Fictional real withdrawal held behind task fence"));
            var deadline = DateTime.UtcNow.AddSeconds(15); var observed = false;
            while (DateTime.UtcNow < deadline)
            {
                observed = await OwnedDatabase.Scalar<long>("SELECT count(*) FROM pg_stat_activity WHERE datname=@db AND application_name='v14-owned-artifact-writer' AND wait_event='advisory'", ("db", OwnedDatabase.DomainName)) == 1;
                if (observed) break;
                await Task.Delay(20);
            }
            Check.That(observed && !writer.IsCompleted, "actual-artifact-writer-blocked-on-same-run-transaction-fence");
        }
        finally
        {
            hold.Released.TrySetResult();
            try { await pending; }
            finally { if (writer is not null) await writer; }
        }
        Check.That((await pending).Succeeded && (await writer!).Succeeded, "task-commit-before-real-artifact-withdrawal");
        var after = await fixture.Read();
        Check.Equal(DatabaseCases.Entry(after, task).Freshness, PlanningTaskFreshness.NeedsReconfirmation, "ordered-later-withdrawal-never-mixed-into-earlier-task-commit");
        await DatabaseCases.NoWrite(fixture, DatabaseCases.Option(after, task), command with { EventId = Guid.NewGuid(), ExpectedRevision = entry.Revision + 1 }, PlanningTaskIssue.SourceConflict);
    }
    private static async Task Rollback()
    {
        var fixture = new DomainFixture(); await fixture.Initialize(); var initial = await fixture.Read();
        var target = initial.Options[0]; await fixture.Review(target); var ready = await fixture.Read(); target = DatabaseCases.Option(ready, target.Identity.TaskId);
        var command = fixture.Command(ready, target); var before = await OwnedDatabase.FullRows(fixture.RunId);
        var failed = false;
        try { var result = await fixture.Store(new Fail()).ApplyAsync(fixture.RunId, target.Identity.TaskId, Policies.Consultant, command); failed = !result.Succeeded; }
        catch (OperationCanceledException) { failed = true; }
        Check.That(failed, "controlled-commit-failure-does-not-accept");
        Check.Equal(await OwnedDatabase.FullRows(fixture.RunId), before, "failure-rolls-back-source-seed-event-current-receipt-atomically");
        var hold = new Hold(); using var cancel = new CancellationTokenSource();
        var pending = fixture.Store(hold).ApplyAsync(fixture.RunId, target.Identity.TaskId, Policies.Consultant, command, cancel.Token);
        var cancelled = false;
        try
        {
            await hold.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            cancel.Cancel();
            try { var result = await pending; cancelled = !result.Succeeded; }
            catch (OperationCanceledException) { cancelled = true; }
        }
        finally
        {
            cancel.Cancel(); hold.Released.TrySetResult();
            try { await pending; } catch (OperationCanceledException) { }
        }
        Check.That(cancelled, "actual-pending-cancel-does-not-accept");
        Check.Equal(await OwnedDatabase.FullRows(fixture.RunId), before, "cancellation-rolls-back-entire-first-task-transaction");
        var accepted = await fixture.Apply(target, command);
        Check.That(accepted.Succeeded && !accepted.AlreadyApplied, "same-original-command-usable-after-full-rollback-no-UUID-reservation");
        var read = await fixture.Store().ReadAsync(fixture.RunId, Policies.Consultant);
        Check.That(read.Succeeded && read.Snapshot!.Entries.Single().Revision == 1, "fresh-instance-reconnect-after-failure-exact-one-creation");
        var replay = await fixture.Store().ApplyAsync(fixture.RunId, target.Identity.TaskId, Policies.Consultant, command);
        Check.That(replay.AlreadyApplied && replay.Receipt == accepted.Receipt, "fresh-instance-reconnect-original-receipt-durable");
    }
}
