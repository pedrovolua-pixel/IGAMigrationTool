using SyntheticPlanningTasks;

internal static class SourceTimelineCases
{
    internal static async Task Run()
    {
        var f = new DomainFixture(); await f.Initialize(); var first = await f.Read(); var original = f.Current;
        var option = first.Options[0]; await f.Review(option); var ready = await f.Read(); option = DatabaseCases.Option(ready, option.Identity.TaskId);
        var command = f.Command(ready, option); var accepted = await f.Apply(option, command);
        Check.That(accepted.Succeeded, "timeline-real-first-planning-event");
        var rows = await OwnedDatabase.FullRows(f.RunId);
        f.Advance(); var advancedInput = f.Current; var observed = await f.Read();
        Check.Equal(await OwnedDatabase.FullRows(f.RunId), rows, "read-later-source-does-not-register-highwater");
        f.Current = original; var returned = await f.Read();
        Check.Equal(DatabaseCases.Entry(returned, option.Identity.TaskId).Freshness, PlanningTaskFreshness.CurrentPlan, "read-only-later-observation-never-registers-source-highwater");
        f.Current = advancedInput; await DatabaseCases.Accepted(f, option.Identity.TaskId, PlanningTaskKind.Comment);
        rows = await OwnedDatabase.FullRows(f.RunId); f.Current = original;
        var rollback = await f.Store().ReadAsync(f.RunId, Policies.Consultant);
        Check.That(rollback.Issue is PlanningTaskIssue.IntegrityMismatch && rollback.Snapshot is null, "accepted-source-full-vector-highwater-rejects-earlier-current-source");
        await DatabaseCases.NoWrite(f, option, command, PlanningTaskIssue.IntegrityMismatch);
        Check.Equal(await OwnedDatabase.FullRows(f.RunId), rows, "rollback-denials-never-register-or-rewrite-proof");
        f.Current = advancedInput; var recovered = await f.Read();
        Check.Equal(DatabaseCases.Entry(recovered, option.Identity.TaskId).Freshness, PlanningTaskFreshness.NeedsReconfirmation, "latest-verifiable-source-recovery-never-resurrects-original-plan");
        // Same text may return at a later finding revision; the complete source remains different.
        f.Advance(restoreText: true); var textReturned = await f.Read();
        Check.Equal(DatabaseCases.Entry(textReturned, option.Identity.TaskId).Freshness, PlanningTaskFreshness.NeedsReconfirmation, "later-text-return-with-new-source-never-resurrects-plan");
        await DatabaseCases.Accepted(f, option.Identity.TaskId, PlanningTaskKind.Comment);
        f.Current = PortableCases.Input("empty") with { Source = original.Source };
        var absent = await f.Store().ReadAsync(f.RunId, Policies.Consultant);
        Check.That(absent.Issue == PlanningTaskIssue.SourceUnavailable && absent.Snapshot?.Source is null, "valid-current-empty-package-with-existing-selected-option-missing-is-unavailable");
        await DatabaseCases.NoWrite(f, option, command, PlanningTaskIssue.SourceUnavailable);
        Check.Group("TC14-T03/T04/T05/T09 actual read-only versus accepted full-source highwater and selected absence");
    }
}
