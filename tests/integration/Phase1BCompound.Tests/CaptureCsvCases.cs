using System.Reflection;
using System.Text.Json;
using AssessmentRuns;
using FindingReview;
using Microsoft.AspNetCore.Http;
using Npgsql;
using SyntheticFixReview;
using SyntheticPlanningTasks;
using SyntheticTaskCsv;

internal static partial class Program
{
    private static async Task CaptureAndCsv(DemoPhase1BService service, SyntheticDurableRunEngine engine, SyntheticReviewStore reviews, SyntheticRunSnapshot run)
    {
        var runId = run.RunId;
        var core = await service.Fenced(runId, (c, t) => service.Core(c, t, runId, Token), Token);
        var candidate = core.Artifacts.Snapshot!.Entries.First().Artifact;
        var finding = core.Review.Snapshot!.Findings.Single(f => f.Seed.FindingId == candidate.FindingId);
        if (finding.Current.State == SyntheticFindingState.Proposed)
            await service.Review(runId, finding.Seed.FindingId, new(Guid.NewGuid(), finding.Current.Revision, SyntheticReviewEventKind.Confirm, "Fictional explicit Consultant review"), Token);
        core = await service.Fenced(runId, (c, t) => service.Core(c, t, runId, Token), Token);
        foreach (var artifact in core.Artifacts.Snapshot!.Entries.Where(e => e.Artifact.ScopedOptionId == candidate.ScopedOptionId))
        {
            var accepted = await service.Artifact(runId, artifact.Artifact.ArtifactId, new(Guid.NewGuid(), ArtifactReviewKind.ReviewForPlanning,
                artifact.Revision, core.ArtifactSource.Binding.SourceDigest, "Fictional explicit three-artifact review"), Token);
            Check(accepted.Succeeded, "actual selected artifact review saved under compound fence");
        }
        var tasks = await service.Fenced(runId, async (c, t) => (await service.Tasks.ReadInTransactionAsync(c, t, runId, DemoPlanningTaskService.Authority)).Snapshot!, Token);
        var option = tasks.Options.Single(o => o.Identity.ScopedOptionId == candidate.ScopedOptionId);
        Check(option.CanCreate && option.CurrentAttestations.Length == 3, "actual guidance/review/source yields exactly three reviewed task attestations");
        var created = await service.TaskEvent(runId, option.Identity.TaskId, new(Guid.NewGuid(), PlanningTaskKind.Create, 0, tasks.Source!.ArtifactSource.SourceDigest, option.CurrentAttestations, "Fictional explicit task planning"), Token);
        Check(created.Succeeded, "actual task creation joins full coherent source transaction");
        foreach (var kind in new[] { PlanningTaskKind.StartProgress, PlanningTaskKind.Complete })
        {
            tasks = await service.Fenced(runId, async (c, t) => (await service.Tasks.ReadInTransactionAsync(c, t, runId, DemoPlanningTaskService.Authority)).Snapshot!, Token);
            var entry = tasks.Entries.Single(); var currentOption = tasks.Options.Single(o => o.Identity.TaskId == entry.Identity.TaskId);
            Check((await service.TaskEvent(runId, entry.Identity.TaskId, new(Guid.NewGuid(), kind, entry.Revision,
                tasks.Source!.ArtifactSource.SourceDigest, currentOption.CurrentAttestations, "Fictional explicit task status change"), Token)).Succeeded, "actual compound task status " + kind);
        }
        var capture = await service.Inspect(runId, Token);
        Check(capture.Rows.Length == 1 && capture.Rows[0].TaskStatus == "Completed" && capture.Rows[0].PlanFreshness == "CurrentPlan", "real completed task remains in current immutable export snapshot");
        Check(capture.SelectedAttestations.Length == 3 && capture.CurrentSourceBinding.FindingRevisions.Any(v => v.FindingId == option.Identity.FindingId), "minimized capture binds reviewed artifacts and actual finding revisions");
        var auditor = new DemoPhase1BService(Connection, engine, reviews, true);
        Check(auditor.ExportAuthority.Role == PlanningTaskExportRole.Auditor && auditor.ExportAuthority.ActorId == "synthetic-auditor", "host supplies genuine Auditor authority without Consultant impersonation");
        var auditorCapture = await auditor.Inspect(runId, Token);
        Check(CsvCanonical.Json(capture) == CsvCanonical.Json(auditorCapture), "genuine authorized Auditor captures all same minimized task metadata");
        await ExpectDenied(async () => await auditor.Workspace(runId, Token), "Auditor cannot enter Consultant protected workspace");
        await ExpectDenied(async () => await auditor.TaskEvent(runId, option.Identity.TaskId, new(Guid.NewGuid(), PlanningTaskKind.Comment, 3,
            tasks.Source!.ArtifactSource.SourceDigest, option.CurrentAttestations, "Denied Auditor mutation"), Token), "Auditor cannot mutate task through export permissions");
        var navigation = JsonSerializer.Serialize(await auditor.Navigate(runId, "tasks", option.Identity.TaskId, Token));
        Check(JsonDocument.Parse(navigation).RootElement.GetProperty("readOnly").GetBoolean() && !navigation.Contains("History", StringComparison.Ordinal) && !navigation.Contains("Fictional explicit task planning", StringComparison.Ordinal), "Auditor protected navigation returns read-only metadata without comments/history");
        await ExpectDenied(async () => await auditor.Navigate(runId, "tasks", new string('f', 64), Token), "unavailable protected task navigation denied");
        foreach (var table in new[] { "outcome", "ai", "review", "artifact", "task" }) await Corruption(service, runId, table);
        Check(CsvCanonical.Json(await service.Inspect(runId, Token)) == CsvCanonical.Json(capture), "all controlled corruption rollbacks restore exact immutable capture");
        var renderer = Environment.GetEnvironmentVariable("PHASE1B_COMPOUND_RENDERER") ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../src/server/workers/SyntheticCsvRenderer/bin/Release/net10.0"));
        var request = Guid.NewGuid(); var pending = await service.Export(runId, request, capture.SnapshotDigest, renderer, Token);
        await Edit(service, runId, option.Identity.FindingId);
        var body = new MemoryStream(); var context = new DefaultHttpContext(); context.Response.Body = body;
        await ExpectDenied(() => pending.ExecuteAsync(context), "source change after render before Execute denies delivery");
        Check(body.Length == 0, "controlled pre-Execute source conflict writes zero response bytes");
        Check(await AuditCount(request, "stage='Denial'") == 1 && await AuditCount(request, "stage='Delivery'") == 0, "source conflict records terminal metadata Denial before any delivery");
        var stale = await service.Inspect(runId, Token);
        Check(stale.Rows.Single().PlanFreshness == "NeedsReconfirmation" && stale.Rows.Single().TaskStatus == "Completed", "changed review/guidance produces stale preserved completed metadata");
        await DeliveryFence(service, engine, reviews, runId, option.Identity.FindingId, renderer);
        var current = await auditor.Inspect(runId, Token); request = Guid.NewGuid(); pending = await auditor.Export(runId, request, current.SnapshotDigest, renderer, Token);
        var cancelled = new DefaultHttpContext(); cancelled.Response.Body = new CancelledWriteStream();
        try { await pending.ExecuteAsync(cancelled); throw new InvalidOperationException("interrupted write succeeded"); }
        catch (OperationCanceledException) { Check(true, "actual host Execute observes response write cancellation"); }
        Check(await AuditCount(request, "stage='Delivery' AND actor_id='synthetic-auditor'") == 1 && await AuditCount(request, "reason='TransferInterrupted' AND actor_id='synthetic-auditor'") == 1,
            "actual Auditor delivery cancellation preserves authorization and appends one typed interruption marker");
        Check(await Scalar("SELECT count(*) FROM synthetic_task_csv.export_audit WHERE request_id=@run AND reason='TransferInterrupted' AND snapshot_digest IS NOT NULL AND output_sha256 IS NOT NULL AND row_count=1", request) == 1,
            "actual interrupted transfer marker retains original snapshot/output/count metadata");
    }
    private static async Task Edit(DemoPhase1BService service, Guid runId, string findingId)
    {
        var core = await service.Fenced(runId, (c, t) => service.Core(c, t, runId, Token), Token);
        var finding = core.Review.Snapshot!.Findings.Single(f => f.Seed.FindingId == findingId);
        await service.Review(runId, findingId, new(Guid.NewGuid(), finding.Current.Revision, SyntheticReviewEventKind.EditPresentation,
            Title: "Controlled title " + Guid.NewGuid().ToString("N"), BusinessContext: "Fictional changed context"), Token);
    }
    private static async Task<long> AuditCount(Guid request, string predicate) => await Scalar("SELECT count(*) FROM synthetic_task_csv.export_audit WHERE request_id=@run AND " + predicate, request);
    private static async Task Corruption(DemoPhase1BService service, Guid runId, string target)
    {
        var sql = target switch
        {
            "outcome" => "UPDATE synthetic_outcome_priority.run_locks SET lock_digest=repeat('0',64) WHERE run_id=@run",
            "ai" => "UPDATE synthetic_ai_execution.event_proof SET digest=repeat('0',64) WHERE event_id IN (SELECT event_id FROM synthetic_ai_execution.events WHERE run_id=@run)",
            "review" => "UPDATE synthetic_review.finding_seeds SET seed_digest=repeat('0',64) WHERE run_id=@run",
            "artifact" => "UPDATE synthetic_fix_review.receipts SET receipt_digest=repeat('0',64) WHERE run_id=@run",
            "task" => "UPDATE synthetic_planning_tasks.receipts SET receipt_digest=repeat('0',64) WHERE run_id=@run",
            _ => throw new InvalidOperationException()
        };
        var table = target switch { "outcome" => "synthetic_outcome_priority.run_locks", "ai" => "synthetic_ai_execution.event_proof", "review" => "synthetic_review.finding_seeds", "artifact" => "synthetic_fix_review.receipts", _ => "synthetic_planning_tasks.receipts" };
        await using var c = new NpgsqlConnection(Connection); await c.OpenAsync(); await using var t = await c.BeginTransactionAsync();
        await using var change = new NpgsqlCommand("ALTER TABLE " + table + " DISABLE TRIGGER USER; " + sql + "; ALTER TABLE " + table + " ENABLE TRIGGER USER", c, t); change.Parameters.AddWithValue("run", runId); await change.ExecuteNonQueryAsync();
        await SyntheticOutcomePriority.SyntheticOutcomePriorityStore.AcquireRegistryFenceAsync(c, t, SyntheticOutcomePriority.OutcomeScope.Fixed, Token);
        var export = typeof(DemoPhase1BService).GetMethod("ExportCapture", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await ExpectIntegrity(async () => await (Task<CsvEnvelope>)export.Invoke(service, [c, t, runId, Token])!, "actual full export denies corrupted " + target + " proof without empty fallback");
        if (target is "outcome" or "ai" or "review") await ExpectIntegrity(async () => await service.Core(c, t, runId, Token), "actual compound projection denies corrupted " + target + " proof");
        await t.RollbackAsync();
    }
    private static async Task ExpectIntegrity(Func<Task> operation, string label)
    {
        try { await operation(); throw new InvalidOperationException("expected proof denial: " + label); }
        catch (Phase1BDeniedException issue) { Check(issue.Message == "IntegrityMismatch", label + " with exact IntegrityMismatch"); }
    }
    private static async Task DeliveryFence(DemoPhase1BService service, SyntheticDurableRunEngine engine, SyntheticReviewStore reviews, Guid runId, string findingId, string renderer)
    {
        var before = await service.Fenced(runId, (c, t) => service.Core(c, t, runId, Token), Token);
        var finding = before.Review.Snapshot!.Findings.Single(f => f.Seed.FindingId == findingId);
        var command = new SyntheticReviewCommand(Guid.NewGuid(), finding.Current.Revision, SyntheticReviewEventKind.EditPresentation,
            Title: "Blocked controlled title " + Guid.NewGuid().ToString("N"), BusinessContext: "Fictional blocked source mutation");
        var capture = await service.Inspect(runId, Token); var request = Guid.NewGuid(); var result = await service.Export(runId, request, capture.SnapshotDigest, renderer, Token);
        var held = new HeldWriteStream(); var context = new DefaultHttpContext(); context.Response.Body = held;
        var execution = result.ExecuteAsync(context);
        await held.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var named = new NpgsqlConnectionStringBuilder(Connection) { ApplicationName = "phase1b-compound-blocked-mutation" };
        var mutationService = new DemoPhase1BService(named.ConnectionString, engine, reviews, false);
        var mutation = mutationService.Review(runId, findingId, command, Token);
        var observed = false; var deadline = DateTimeOffset.UtcNow.AddSeconds(3);
        while (!observed && DateTimeOffset.UtcNow < deadline)
        {
            observed = await Scalar("SELECT count(*) FROM pg_locks l JOIN pg_stat_activity a ON a.pid=l.pid WHERE a.datname=current_database() AND a.application_name='phase1b-compound-blocked-mutation' AND l.locktype='advisory' AND NOT l.granted AND @run IS NOT NULL", runId) > 0;
            if (!observed) await Task.Delay(25);
        }
        try
        {
            Check(observed && !mutation.IsCompleted, "actual PostgreSQL source mutation waits on session fence during response write");
            Check(await AuditCount(request, "stage='Delivery'") == 1, "authorization audit committed while first output write holds source exclusion");
        }
        finally { held.Release.TrySetResult(); }
        await execution; await mutation;
        var expected = CsvCodec.Render(capture);
        Check(held.ToArray().AsSpan().SequenceEqual(expected), "fenced host delivers exact immutable CSV bytes despite waiting source mutation");
        Check(context.Response.Headers.CacheControl == "no-store" && context.Response.ContentLength == expected.Length, "actual response sets no-store and exact bounded byte length");
        Check((await service.Inspect(runId, Token)).SnapshotDigest != capture.SnapshotDigest, "source mutation commits only after response write releases fence");
        Check(await AuditCount(request, "stage='Denial'") == 0, "successful delivery has no fabricated interruption marker");
    }
}
internal sealed class HeldWriteStream : MemoryStream
{
    internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
    { Entered.TrySetResult(); await Release.Task.WaitAsync(ct); await base.WriteAsync(buffer, ct); }
}
internal sealed class CancelledWriteStream : MemoryStream
{
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) => ValueTask.FromCanceled(new CancellationToken(true));
}
