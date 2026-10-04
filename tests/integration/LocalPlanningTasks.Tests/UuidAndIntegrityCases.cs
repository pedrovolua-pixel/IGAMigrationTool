using Npgsql;
using SyntheticPlanningTasks;

internal static class UuidAndIntegrityCases
{
    internal static async Task Run()
    {
        var f = new DomainFixture(); await f.Initialize(); var read = await f.Read(); var a = read.Options[0]; var b = read.Options[1];
        await f.Review(a); await f.Review(b); read = await f.Read(); a = DatabaseCases.Option(read, a.Identity.TaskId); b = DatabaseCases.Option(read, b.Identity.TaskId);
        var first = f.Command(read, a); Check.That((await f.Apply(a, first)).Succeeded, "uuid-first-real-accepted-owner-command");
        await DatabaseCases.NoWrite(f, b, f.Command(read, b, eventId: first.EventId), PlanningTaskIssue.EventConflict);
        var another = new DomainFixture(); await another.Initialize(); var otherRead = await another.Read(); var other = otherRead.Options[0]; await another.Review(other); otherRead = await another.Read(); other = DatabaseCases.Option(otherRead, other.Identity.TaskId);
        Check.That((await another.Apply(other, another.Command(otherRead, other, eventId: first.EventId))).Succeeded, "accepted-taskUUID-namespace-scoped-to-run-not-global");
        var before = await OwnedDatabase.FullRows(f.RunId);
        foreach (var table in new[] { "events", "receipts", "source_versions", "task_seeds" })
        {
            var denied = false;
            try { await OwnedDatabase.Sql("DELETE FROM synthetic_planning_tasks." + table + " WHERE run_id=@run", ("run", f.RunId)); }
            catch (PostgresException error) when (error.SqlState == "55000") { denied = true; }
            Check.That(denied, "actual-immutable-task-proof-event-receipt-seed-delete-denied");
        }
        Check.Equal(await OwnedDatabase.FullRows(f.RunId), before, "actual-immutable-denials-full-rows-invariant");
        var original = await OwnedDatabase.Scalar<string>("SELECT current_json FROM synthetic_planning_tasks.task_current WHERE run_id=@run AND task_id=@task", ("run", f.RunId), ("task", a.Identity.TaskId));
        try
        {
            await OwnedDatabase.Sql("ALTER TABLE synthetic_planning_tasks.task_current DISABLE TRIGGER current_revision_guard");
            await OwnedDatabase.Sql("UPDATE synthetic_planning_tasks.task_current SET current_json='{}' WHERE run_id=@run AND task_id=@task", ("run", f.RunId), ("task", a.Identity.TaskId));
            await OwnedDatabase.Sql("ALTER TABLE synthetic_planning_tasks.task_current ENABLE TRIGGER current_revision_guard");
            var corrupt = await f.Store().ReadAsync(f.RunId, Policies.Consultant); Check.That(corrupt.Issue == PlanningTaskIssue.IntegrityMismatch && corrupt.Snapshot is null, "actual-corrupt-task-owned-row-never-source-unavailable-metadata");
            var result = await f.Apply(a, first); Check.That(result.Issue == PlanningTaskIssue.IntegrityMismatch && result.Receipt is null, "corrupt-current-row-denies-even-exact-accepted-replay");
        }
        finally
        {
            await OwnedDatabase.Sql("ALTER TABLE synthetic_planning_tasks.task_current DISABLE TRIGGER current_revision_guard");
            try { await OwnedDatabase.Sql("UPDATE synthetic_planning_tasks.task_current SET current_json=@value WHERE run_id=@run AND task_id=@task", ("value", original!), ("run", f.RunId), ("task", a.Identity.TaskId)); }
            finally { await OwnedDatabase.Sql("ALTER TABLE synthetic_planning_tasks.task_current ENABLE TRIGGER current_revision_guard"); }
        }
        Check.Equal(await OwnedDatabase.FullRows(f.RunId), before, "corrupt-current-row-original-complete-byte-restoration");
        try
        {
            await OwnedDatabase.Sql("ALTER TABLE synthetic_planning_tasks.events DISABLE TRIGGER event_immutable");
            var drift = await f.Store().ReadAsync(f.RunId, Policies.Consultant); Check.That(drift.Issue == PlanningTaskIssue.MigrationDrift && drift.Snapshot is null, "actual-task-trigger-state-fingerprint-denial-no-history");
        }
        finally { await OwnedDatabase.Sql("ALTER TABLE synthetic_planning_tasks.events ENABLE TRIGGER event_immutable"); }
        Check.Equal(await OwnedDatabase.Scalar<string>("SELECT tgenabled::text FROM pg_trigger WHERE tgrelid='synthetic_planning_tasks.events'::regclass AND tgname='event_immutable'"), "O", "original-task-trigger-state-restored");
        Check.That((await f.Apply(a, first)).AlreadyApplied, "restored-task-store-original-receipt-recoverable");
        Check.Group("TC14-T02/T04/T05/T06/T12 actual run-wide UUID namespace, immutable records, corruption and schema recovery");
    }
}
