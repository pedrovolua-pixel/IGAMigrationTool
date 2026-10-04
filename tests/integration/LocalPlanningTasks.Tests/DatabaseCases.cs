using System.Text.Json;
using System.Text.Json.Nodes;
using SyntheticFixReview;
using SyntheticPlanningTasks;

internal static class DatabaseCases
{
    internal static string Canonical(object value) => Expected.Canonical(JsonSerializer.SerializeToNode(value, V14Program.Web));
    internal static async Task Run()
    {
        OwnedDatabase.Configure(); await OwnedDatabase.CreateAbsentOnly();
        await SourceBoundaryCases.Run();
        await LifecycleReplayAndFreshness();
        await AuthorityAndUnavailable();
        await UuidAndIntegrityCases.Run();
        await SourceTimelineCases.Run();
        await ConcurrencyRecoveryCases.Run();
        await SavedSourceCases.Run();
        await HostRecoveryCases.Run();
        Check.Group("TC14-T01–T06/T08/T11/T12 actual owned PostgreSQL task composition");
    }
    internal static PlanningTaskOption Option(PlanningTaskSnapshot snapshot, string task) => snapshot.Options.Single(o => o.Identity.TaskId == task);
    internal static PlanningTaskEntry Entry(PlanningTaskSnapshot snapshot, string task) => snapshot.Entries.Single(e => e.Identity.TaskId == task);
    internal static async Task NoWrite(DomainFixture fixture, PlanningTaskOption option, PlanningTaskCommand command, PlanningTaskIssue issue, PlanningTaskAuthority? authority = null)
    {
        var before = await OwnedDatabase.FullRows(fixture.RunId);
        var result = await fixture.Apply(option, command, authority);
        if (result.Issue != issue)
            Console.WriteLine($"DIAGNOSTIC denial-issue; expected={issue}; actual={result.Issue}; foreign-actor={authority?.ActorId != null && authority.ActorId != Policies.Consultant.ActorId}");
        Check.Equal(result.Issue, issue, "exact-denial-issue-precedence");
        Check.That(result.Receipt is null && result.AlreadyExistsTaskId is null && !result.AlreadyApplied, "denial-no-receipt-identity-or-replay-leak");
        Check.Equal(await OwnedDatabase.FullRows(fixture.RunId), before, "denial-complete-task-and-artifact-stores-byte-invariant");
    }
    private static void Receipt(PlanningTaskApplyResult result, DomainFixture fixture, PlanningTaskOption option, PlanningTaskCommand command, long revision)
    {
        Check.That(result.Succeeded && result.Issue is null && result.Receipt is not null && result.AlreadyExistsTaskId is null && !result.AlreadyApplied, "accepted-command-distinct-from-replay-or-duplicate");
        var r = result.Receipt!;
        var expected = new PlanningTaskReceipt("synthetic-planning-task-receipt-v1", command.EventId, fixture.RunId, option.Identity.TaskId,
            command.Kind, revision, "synthetic-consultant", r.RecordedAtUtc, command.ExpectedSourceDigest);
        Check.Equal(Expected.Canonical(JsonSerializer.SerializeToNode(r, V14Program.Web)), Expected.Canonical(JsonSerializer.SerializeToNode(expected, V14Program.Web)), "complete-independent-metadata-receipt-fields");
        Check.That(r.RecordedAtUtc.Offset == TimeSpan.Zero && r.RecordedAtUtc <= DateTimeOffset.UtcNow && r.RecordedAtUtc >= DateTimeOffset.UtcNow.AddMinutes(-2), "actual-server-recorded-time-UTC-and-bounded-window");
    }
    internal static async Task<PlanningTaskApplyResult> Accepted(DomainFixture fixture, string task, PlanningTaskKind kind, string? reason = null)
    {
        var read = await fixture.Read(); fixture.Verify(read);
        var option = Option(read, task); var revision = read.Entries.SingleOrDefault(e => e.Identity.TaskId == task)?.Revision ?? 0;
        var command = fixture.Command(read, option, kind, revision, reason: reason);
        var result = await fixture.Apply(option, command); Receipt(result, fixture, option, command, revision + 1);
        var after = await fixture.Read(); var entry = Entry(after, task);
        Check.Equal(entry.Revision, revision + 1, "accepted-next-continuous-task-revision");
        Check.Equal(entry.History.Length, (int)entry.Revision, "continuous-history-no-gaps");
        Check.Equal(entry.History[^1].Reason, command.Reason, "exact-original-command-text-preserved");
        Check.Equal(entry.History[^1].EventId, command.EventId, "accepted-eventUUID-attributed-history");
        Check.Equal(entry.History[^1].PlanningEventId, entry.Plan.EventId, "every-event-references-latest-planning-binding");
        Check.That(entry.History.All(e => e.ActorId == "synthetic-consultant" && e.ActorRoles.SequenceEqual(["Consultant"])), "continuous-trusted-consultant-attribution");
        Check.Equal(entry.Creation.Kind, PlanningTaskKind.Create, "immutable-creation-event-kind");
        return result;
    }
    private static async Task LifecycleReplayAndFreshness()
    {
        var fixture = new DomainFixture(); await fixture.Initialize();
        var initial = await fixture.Read(); fixture.Verify(initial);
        Check.Equal(fixture.Calls, 1, "actual-source-captured-exactly-once-per-task-read");
        Check.That(initial.Entries.IsEmpty && initial.Options.All(o => !o.CanCreate), "unreviewed-real-artifacts-no-auto-conversion");
        var target = initial.Options[0]; var task = target.Identity.TaskId;
        await NoWrite(fixture, target, fixture.Command(initial, target), PlanningTaskIssue.InvalidState);
        await fixture.Review(target);
        var ready = await fixture.Read(); target = Option(ready, task);
        Check.That(target.CanCreate && target.CurrentAttestations.All(a => a.State == ArtifactReviewState.ReviewedForPlanning), "complete-three-real-current-reviewed-eligibility");
        var creation = fixture.Command(ready, target); var created = await fixture.Apply(target, creation); Receipt(created, fixture, target, creation, 1);
        var first = Entry(await fixture.Read(), task);
        Check.That(first.Status == PlanningTaskStatus.Planned && first.Freshness == PlanningTaskFreshness.CurrentPlan && first.Creation == first.Plan, "actual-first-planned-current-creation-binding");
        await NoWrite(fixture, target, fixture.Command(ready, target, PlanningTaskKind.ReconfirmPlan, 1), PlanningTaskIssue.InvalidState);
        var progressed = await Accepted(fixture, task, PlanningTaskKind.StartProgress);
        var current = await fixture.Read(); var beforeDuplicate = await OwnedDatabase.FullRows(fixture.RunId);
        var duplicateId = Guid.NewGuid(); var duplicate = fixture.Command(current, Option(current, task), eventId: duplicateId);
        var exists = await fixture.Apply(Option(current, task), duplicate);
        Check.That(exists.Succeeded && exists.Issue is null && exists.AlreadyExistsTaskId == task && exists.Receipt is null && !exists.AlreadyApplied, "fresh-duplicate-revision0-after-task-revision2-typed-identity-only");
        Check.Equal(await OwnedDatabase.FullRows(fixture.RunId), beforeDuplicate, "duplicate-no-event-receipt-UUID-source-current-seed-write");
        var comment = fixture.Command(current, Option(current, task), PlanningTaskKind.Comment, 2, duplicateId);
        Receipt(await fixture.Apply(Option(current, task), comment), fixture, Option(current, task), comment, 3);
        var beforeReplay = await OwnedDatabase.FullRows(fixture.RunId);
        var replay = await fixture.Apply(target, creation);
        Check.That(replay.Succeeded && replay.AlreadyApplied && replay.Receipt == created.Receipt && replay.AlreadyExistsTaskId is null, "accepted-create-replay-before-duplicate-original-historical-metadata");
        Check.Equal(await OwnedDatabase.FullRows(fixture.RunId), beforeReplay, "replay-no-old-state-binding-restoration-or-write");
        await NoWrite(fixture, target, creation with { Reason = creation.Reason + " changed" }, PlanningTaskIssue.EventConflict);
        await NoWrite(fixture, target, creation with { ExpectedRevision = 1 }, PlanningTaskIssue.InvalidInput);
        // An unselected attestation-only write must not stale the chosen option.
        var unselected = current.Options.First(o => o.Identity.TaskId != task);
        await fixture.Review(unselected);
        Check.Equal(Entry(await fixture.Read(), task).Freshness, PlanningTaskFreshness.CurrentPlan, "unselected-attestation-only-change-does-not-stale");
        await fixture.Withdraw(target.Identity.ArtifactIds[0]);
        var withdrawn = await fixture.Read();
        Check.Equal(Entry(withdrawn, task).Freshness, PlanningTaskFreshness.NeedsReconfirmation, "selected-withdrawal-stales-with-unchanged-full-package");
        Check.Equal(Entry(withdrawn, task).Status, PlanningTaskStatus.InProgress, "freshness-does-not-change-workflow");
        await NoWrite(fixture, Option(withdrawn, task), duplicate with { EventId = Guid.NewGuid() }, PlanningTaskIssue.SourceConflict);
        var withdrawnDuplicate = fixture.Command(withdrawn, Option(withdrawn, task));
        beforeDuplicate = await OwnedDatabase.FullRows(fixture.RunId);
        exists = await fixture.Apply(Option(withdrawn, task), withdrawnDuplicate);
        Check.That(exists.AlreadyExistsTaskId == task && exists.Receipt is null && !exists.AlreadyApplied, "inspected-withdrawn-proof-existing-task-duplicate-no-eligibility-recheck");
        Check.Equal(await OwnedDatabase.FullRows(fixture.RunId), beforeDuplicate, "withdrawn-duplicate-no-stores-change");
        replay = await fixture.Apply(target, creation);
        Check.That(replay.AlreadyApplied && replay.Receipt == created.Receipt, "exact-accepted-create-replay-after-selected-withdrawal");
        await NoWrite(fixture, Option(withdrawn, task), fixture.Command(withdrawn, Option(withdrawn, task), PlanningTaskKind.Complete, 3), PlanningTaskIssue.InvalidState);
        await Accepted(fixture, task, PlanningTaskKind.Comment);
        await fixture.Review(Option(withdrawn, task));
        Check.Equal(Entry(await fixture.Read(), task).Freshness, PlanningTaskFreshness.NeedsReconfirmation, "withdraw-rereview-unchanged-text-does-not-reconfirm");
        var reconfirm = await Accepted(fixture, task, PlanningTaskKind.ReconfirmPlan);
        var confirmed = Entry(await fixture.Read(), task);
        Check.That(confirmed.Freshness == PlanningTaskFreshness.CurrentPlan && confirmed.Status == PlanningTaskStatus.InProgress && Canonical(confirmed.Creation) == Canonical(first.Creation), "explicit-reconfirm-preserves-creation-and-workflow");
        await Accepted(fixture, task, PlanningTaskKind.Complete);
        await Accepted(fixture, task, PlanningTaskKind.Comment);
        fixture.Advance();
        var staleTerminal = Entry(await fixture.Read(), task);
        Check.That(staleTerminal.Status == PlanningTaskStatus.Completed && staleTerminal.Freshness == PlanningTaskFreshness.NeedsReconfirmation, "source-change-completed-stays-terminal-but-stale");
        await Accepted(fixture, task, PlanningTaskKind.Reopen);
        Check.Equal(Entry(await fixture.Read(), task).Freshness, PlanningTaskFreshness.NeedsReconfirmation, "explicit-reopen-does-not-rebind");
        await fixture.Review(Option(await fixture.Read(), task));
        await Accepted(fixture, task, PlanningTaskKind.ReconfirmPlan);
        await Accepted(fixture, task, PlanningTaskKind.Cancel);
        await Accepted(fixture, task, PlanningTaskKind.Comment);
        fixture.Advance(rejected: true);
        var rejected = await fixture.Read(); var rejectedOption = Option(rejected, task); var rejectedEntry = Entry(rejected, task);
        await NoWrite(fixture, rejectedOption, fixture.Command(rejected, rejectedOption, PlanningTaskKind.Reopen, rejectedEntry.Revision), PlanningTaskIssue.InvalidState);
        await Accepted(fixture, task, PlanningTaskKind.Comment);
        rejected = await fixture.Read(); rejectedOption = Option(rejected, task);
        beforeDuplicate = await OwnedDatabase.FullRows(fixture.RunId);
        exists = await fixture.Apply(rejectedOption, fixture.Command(rejected, rejectedOption));
        Check.That(exists.AlreadyExistsTaskId == task && exists.Receipt is null, "inspected-Rejected-existing-task-AlreadyExists");
        Check.Equal(await OwnedDatabase.FullRows(fixture.RunId), beforeDuplicate, "Rejected-duplicate-complete-stores-invariant");
        replay = await fixture.Apply(target, creation);
        Check.That(replay.AlreadyApplied && replay.Receipt == created.Receipt, "accepted-historical-replay-after-Rejected-terminal-current-proof");
        Check.Equal(Canonical(Entry(await fixture.Read(), task).Creation), Canonical(first.Creation), "immutable-original-creation-through-entire-workflow");
        Check.That(progressed.Receipt != reconfirm.Receipt, "distinct-attributed-status-and-binding-receipts");
        Check.Group("TC14-T01/T03/T04/T11 actual conversion/workflow/selected-freshness/duplicate/replay");
    }
    private static async Task AuthorityAndUnavailable()
    {
        var fixture = new DomainFixture(); await fixture.Initialize();
        var initial = await fixture.Read(); var target = initial.Options[0]; await fixture.Review(target);
        var ready = await fixture.Read(); target = Option(ready, target.Identity.TaskId);
        var creation = fixture.Command(ready, target); var accepted = await fixture.Apply(target, creation);
        Check.That(accepted.Succeeded, "authority-fixture-one-real-task");
        foreach (var bad in Policies.InvalidAuthorities())
            await NoWrite(fixture, target, creation, PlanningTaskIssue.Denied, bad);
        await NoWrite(fixture, target, creation, PlanningTaskIssue.Denied, Policies.Consultant with { ActorId = "foreign-consultant" });
        var foreignRead = await fixture.Store().ReadAsync(fixture.RunId, Policies.Consultant with { ActorId = "foreign-consultant" });
        Check.That(foreignRead.Issue == PlanningTaskIssue.Denied && foreignRead.Snapshot is null, "foreign-assignee-read-denied-before-historical-content");
        fixture.Unavailable = true;
        var before = await OwnedDatabase.FullRows(fixture.RunId);
        var read = await fixture.Store().ReadAsync(fixture.RunId, Policies.Consultant);
        Check.That(read.Issue == PlanningTaskIssue.SourceUnavailable && read.Snapshot is not null && read.Snapshot.Source is null && read.Snapshot.Options.IsEmpty, "verified-owned-unavailable-task-read-bounded-metadata");
        Check.That(read.Snapshot!.Entries.All(e => e.Freshness == PlanningTaskFreshness.SourceUnavailable && !e.CanReconfirm && !e.CanStart && !e.CanReturnToPlanned && !e.CanComplete && !e.CanCancel && !e.CanReopen && !e.CanComment), "unavailable-source-all-store-action-flags-false");
        await NoWrite(fixture, target, creation, PlanningTaskIssue.SourceUnavailable);
        foreach (var kind in Enum.GetValues<PlanningTaskKind>())
            await NoWrite(fixture, target, creation with { EventId = Guid.NewGuid(), Kind = kind, ExpectedRevision = kind == PlanningTaskKind.Create ? 0 : 1 }, PlanningTaskIssue.SourceUnavailable);
        Check.Equal(await OwnedDatabase.FullRows(fixture.RunId), before, "unavailable-read-and-all-actions-complete-stores-invariant");
        fixture.Unavailable = false;
        var replay = await fixture.Apply(target, creation);
        Check.That(replay.AlreadyApplied && replay.Receipt == accepted.Receipt, "proof-recovery-original-replay-still-exact");
        Check.Group("TC14-T02/T04/T11/T12 authority-before-replay and unavailable-all-writes");
    }
}
