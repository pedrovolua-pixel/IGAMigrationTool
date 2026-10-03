using AssessmentRuns;
using FindingReview;
using Npgsql;
using SyntheticFixReview;
using SyntheticPlanningTasks;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class HostRecoveryCases
{
    internal static async Task Run()
    {
        var run = await SavedSourceCases.Complete("synthetic-analysis-findings-v1", Expected.Profile);
        var reviews = new DemoReviewService(new SyntheticReviewStore(OwnedDatabase.Connection, SyntheticReviewScope.Fixed));
        var engine = SavedSourceCases.Engine(); var host = new DemoPlanningTaskService(OwnedDatabase.Connection, engine, reviews);
        var ready = await host.ReadAsync(run.RunId); Check.That(ready.Succeeded, "actual-parent-host-current-capture-ready");
        var option = ready.Snapshot!.Options[0];
        var artifacts = SavedSourceCases.ArtifactStore();
        foreach (var id in option.Identity.ArtifactIds)
        {
            var read = await artifacts.ReadAsync(run.RunId, Policies.ArtifactConsultant); var entry = read.Snapshot!.Entries.Single(e => e.Artifact.ArtifactId == id);
            var result = await artifacts.ApplyAsync(run.RunId, id, Policies.ArtifactConsultant, new(Guid.NewGuid(), ArtifactReviewKind.ReviewForPlanning, entry.Revision, read.Snapshot.Source.SourceDigest, "Fictional host recovery precondition"));
            Check.That(result.Succeeded, "actual-parent-host-real-artifact-review");
        }
        ready = await host.ReadAsync(run.RunId); option = DatabaseCases.Option(ready.Snapshot!, option.Identity.TaskId);
        var command = new PlanningTaskCommand(Guid.NewGuid(), PlanningTaskKind.Create, 0, ready.Snapshot!.Source!.ArtifactSource.SourceDigest, option.CurrentAttestations, "Fictional exact host recovery reason");
        var created = await host.ApplyAsync(run.RunId, option.Identity.TaskId, command); Check.That(created.Succeeded, "actual-parent-host-real-task-creation");
        foreach (var column in new[] { "plan_json", "versions_json" })
        {
            var original = await OwnedDatabase.Scalar<string>("SELECT " + column + " FROM synthetic_assessment.runs WHERE run_id=@run", ("run", run.RunId));
            var before = await OwnedDatabase.FullRows(run.RunId);
            var denied = false;
            try { await OwnedDatabase.Sql("UPDATE synthetic_assessment.runs SET " + column + "='{' WHERE run_id=@run", ("run", run.RunId)); }
            catch (PostgresException error) when (error.SqlState == "P0001") { denied = true; }
            Check.That(denied, "actual-original-run-lock-trigger-denies-fixture-tamper");
            try
            {
                await OwnedDatabase.Sql("ALTER TABLE synthetic_assessment.runs DISABLE TRIGGER immutable_run_inputs");
                await OwnedDatabase.Sql("UPDATE synthetic_assessment.runs SET " + column + "='{' WHERE run_id=@run", ("run", run.RunId));
                await OwnedDatabase.Sql("ALTER TABLE synthetic_assessment.runs ENABLE TRIGGER immutable_run_inputs");
                var read = await host.ReadAsync(run.RunId);
                Check.That(read.Issue == PlanningTaskIssue.SourceUnavailable && read.Snapshot?.Entries.Length == 1, "actual-malformed-dependency-json-retains-verified-task-metadata");
                var detail = JsonSerializer.SerializeToNode(DemoPlanningTaskService.Detail(read), V14Program.Web)!.AsObject();
                Check.That(detail["status"]!.GetValue<string>() == "SourceUnavailable" && detail["source"] is null && detail["options"]!.AsArray().Count == 0 && detail["entries"]!.AsArray().Count == 0, "actual-source-unavailable-public-detail-suppresses-source-and-content");
                var metadata = detail["unavailableEntries"]!.AsArray().Single()!.AsObject();
                Check.That(metadata.Count == 6 && !metadata.ToJsonString().Contains(command.Reason, StringComparison.Ordinal), "actual-unavailable-metadata-closed-no-reason-source-artifact-text");
                var applied = await host.ApplyAsync(run.RunId, option.Identity.TaskId, command);
                Check.That(applied.Issue == PlanningTaskIssue.SourceUnavailable && applied.Receipt is null, "malformed-current-dependency-denies-even-exact-accepted-replay");
                Check.Equal(await OwnedDatabase.FullRows(run.RunId), before, "malformed-dependency-read-replay-task-stores-invariant");
            }
            finally
            {
                await OwnedDatabase.Sql("ALTER TABLE synthetic_assessment.runs DISABLE TRIGGER immutable_run_inputs");
                try { await OwnedDatabase.Sql("UPDATE synthetic_assessment.runs SET " + column + "=@value WHERE run_id=@run", ("value", original!), ("run", run.RunId)); }
                finally { await OwnedDatabase.Sql("ALTER TABLE synthetic_assessment.runs ENABLE TRIGGER immutable_run_inputs"); }
                Check.Equal(await OwnedDatabase.Scalar<string>("SELECT " + column + " FROM synthetic_assessment.runs WHERE run_id=@run", ("run", run.RunId)), original, "exact-original-dependency-json-restored");
                Check.Equal(await OwnedDatabase.Scalar<string>("SELECT tgenabled::text FROM pg_trigger WHERE tgrelid='synthetic_assessment.runs'::regclass AND tgname='immutable_run_inputs'"), "O", "owned-run-trigger-enabled-after-all-recovery-paths");
            }
            Check.That((await host.ReadAsync(run.RunId)).Succeeded && (await host.ApplyAsync(run.RunId, option.Identity.TaskId, command)).AlreadyApplied, "restored-parent-host-source-and-original-receipt-recover");
        }
        Check.Group("TC14-T07/T12 actual parent host malformed persisted dependency recovery and metadata suppression");
    }
}
