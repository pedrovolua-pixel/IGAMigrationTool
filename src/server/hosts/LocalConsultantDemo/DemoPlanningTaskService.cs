using System.Text.Json;
using AssessmentRuns;
using FindingReview;
using Npgsql;
using SyntheticFixReview;
using SyntheticPlanningTasks;

/// <summary>Request-scoped fenced composition; source/table ownership remains within each module.</summary>
internal sealed class DemoPlanningTaskService(string connection, SyntheticDurableRunEngine engine, DemoReviewService reviews)
{
    internal static PlanningTaskAuthority Authority { get; } = new("synthetic-consultant", true, true, false, true,
        PlanningTaskScope.Fixed, [PlanningTaskRole.Consultant], ["SECURITY", "OPERATIONS"],
        [PlanningTaskAction.Read, PlanningTaskAction.Create, PlanningTaskAction.Manage, PlanningTaskAction.Comment], PlanningTaskResourceState.Mutable);
    internal Task InitializeAsync(CancellationToken ct = default) => Store(_ => { }).InitializeAsync(ct);
    internal async Task<object?> ReadAnalysisAsync(SyntheticRunSnapshot initial, CancellationToken ct = default)
    {
        // Existing finding seeds are committed before entering the source-capture transaction.
        try { await reviews.ReadAsync(initial, ct); }
        catch (Exception error) when (SourceDecodingFailure(error)) { return null; }
        DemoAnalysisCapture? capture = null;
        var read = await Store(value => capture = value).ReadAsync(initial.RunId, Authority, ct);
        if (capture is null) return null;
        capture.Analysis["planningTasks"] = JsonSerializer.SerializeToNode(Detail(read), DemoReportDraftProjection.JsonOptions);
        return capture.Analysis;
    }
    internal async Task<PlanningTaskReadResult> ReadAsync(Guid runId, CancellationToken ct = default)
    {
        try
        {
            var initial = await engine.ReadAsync(DemoFixtureCatalog.Scope, runId, ct);
            if (initial.Succeeded)
            {
                if (!DemoPlanningTaskCatalog.IsProfile(initial.Snapshot!.ProfileCatalogId)) return new(PlanningTaskIssue.Denied, null);
                if (DemoPlanningTaskCatalog.MatchesFrozenFixture(initial.Snapshot)) await reviews.ReadAsync(initial.Snapshot, ct);
            }
        }
        catch (Exception error) when (SourceDecodingFailure(error))
        {
            // Only the dependency failed; the fenced task reader still verifies its own stored proof.
        }
        return await Store(_ => { }).ReadAsync(runId, Authority, ct);
    }
    internal Task<PlanningTaskApplyResult> ApplyAsync(Guid runId, string taskId, PlanningTaskCommand command, CancellationToken ct = default) =>
        Store(_ => { }).ApplyAsync(runId, taskId, Authority, command, ct);
    private SyntheticPlanningTaskStore Store(Action<DemoAnalysisCapture> captured) => new(connection, PlanningTaskScope.Fixed,
        async (db, tx, runId, authority, ct) =>
        {
            try
            {
                var run = await engine.ReadInTransactionAsync(db, tx, DemoFixtureCatalog.Scope, runId, ct);
                if (!run.Succeeded || !DemoPlanningTaskCatalog.MatchesFrozenFixture(run.Snapshot)) return new(PlanningTaskIssue.SourceUnavailable, null);
                var review = await reviews.ReadExistingInTransactionAsync(db, tx, run.Snapshot!, ct);
                if (review.Snapshot is null) return new(PlanningTaskIssue.SourceUnavailable, null);
                var value = DemoAnalysisProjection.Capture(run.Snapshot!, review);
                var artifactSource = ArtifactReviewSourceBuilder.Build(value.FixPackages?.Snapshot);
                if (!artifactSource.Succeeded) return new(PlanningTaskIssue.SourceUnavailable, null);
                var artifacts = await new SyntheticFixReviewStore(connection, ArtifactReviewScope.Fixed,
                    (_, _) => Task.FromResult(new ArtifactReviewSourceResult(ArtifactReviewIssue.SourceUnavailable, null)))
                    .ReadInTransactionAsync(db, tx, runId, DemoArtifactReviewService.Authority, artifactSource.Source!, ct);
                if (!artifacts.Succeeded) return new(artifacts.Issue is ArtifactReviewIssue.Denied or ArtifactReviewIssue.WrongScope
                    ? PlanningTaskIssue.Denied : PlanningTaskIssue.SourceUnavailable, null);
                if (artifacts.Snapshot!.ActorId != authority.ActorId) return new(PlanningTaskIssue.Denied, null);
                value.Analysis["artifactReview"] = JsonSerializer.SerializeToNode(DemoArtifactReviewService.Detail(artifacts), DemoReportDraftProjection.JsonOptions);
                captured(value);
                return PlanningTaskSourceBuilder.Build(value.FixPackages!.Snapshot, artifacts.Snapshot);
            }
            catch (Exception error) when (SourceDecodingFailure(error))
            {
                return new(PlanningTaskIssue.SourceUnavailable, null);
            }
        });
    private static bool SourceDecodingFailure(Exception error) => error is JsonException or SyntheticRunIntegrityException or SyntheticReviewIntegrityException or FormatException or OverflowException;
    internal static object Detail(PlanningTaskReadResult read)
    {
        var unavailable = read.Issue == PlanningTaskIssue.SourceUnavailable && read.Snapshot is not null;
        return new
        {
            schemaVersion = 1,
            demoOnly = true,
            status = read.Succeeded ? "Ready" : unavailable ? "SourceUnavailable" : "Unavailable",
            reasonCode = read.Succeeded ? null : unavailable ? "planning_task_source_unavailable" : read.Issue switch
            {
                PlanningTaskIssue.Denied or PlanningTaskIssue.WrongScope => "planning_task_denied",
                PlanningTaskIssue.IntegrityMismatch or PlanningTaskIssue.MigrationDrift or PlanningTaskIssue.SeedConflict => "planning_task_integrity_denied",
                _ => "planning_task_source_unavailable"
            },
            source = read.Succeeded ? read.Snapshot!.Source : null,
            actorId = read.Succeeded || unavailable ? read.Snapshot!.ActorId : null,
            options = read.Succeeded ? read.Snapshot!.Options.OrderBy(o => o.Identity.TaskId, StringComparer.Ordinal).Select(o => new
            { o.Identity, o.FindingState, currentAttestations = o.CurrentAttestations.Select(Attestation).ToArray(), o.CanCreate }).ToArray() : [],
            entries = read.Succeeded ? read.Snapshot!.Entries.OrderBy(e => e.Identity.TaskId, StringComparer.Ordinal).Select(e => new
            {
                e.Identity,
                e.AssigneeId,
                e.Revision,
                status = e.Status.ToString(),
                freshness = e.Freshness.ToString(),
                creation = Event(e.Creation),
                plan = Event(e.Plan),
                history = e.History.Select(Event).ToArray(),
                e.CanReconfirm,
                e.CanStart,
                e.CanReturnToPlanned,
                e.CanComplete,
                e.CanCancel,
                e.CanReopen,
                e.CanComment
            }).ToArray() : [],
            unavailableEntries = unavailable ? read.Snapshot!.Entries.OrderBy(e => e.Identity.TaskId, StringComparer.Ordinal).Select(e => new
            {
                e.Identity,
                e.AssigneeId,
                e.Revision,
                status = e.Status.ToString(),
                freshness = "SourceUnavailable",
                history = e.History.Select(h => new
                { h.EventId, h.Revision, kind = h.Kind.ToString(), h.ActorId, h.ActorRoles, recordedAtUtc = h.RecordedAtUtc.UtcDateTime, recordedStatus = h.RecordedStatus.ToString(), h.PlanningEventId }).ToArray()
            }).ToArray() : []
        };
    }
    private static object Attestation(PlanningTaskAttestation a) => new
    { a.ArtifactId, a.Revision, a.EventId, kind = a.Kind?.ToString(), state = a.State.ToString(), a.SourceDigest };
    private static object Event(PlanningTaskEvent e) => new
    {
        e.EventId,
        e.Revision,
        kind = e.Kind.ToString(),
        e.ActorId,
        e.ActorRoles,
        recordedAtUtc = e.RecordedAtUtc.UtcDateTime,
        e.Reason,
        e.Source,
        attestations = e.Attestations.Select(Attestation).ToArray(),
        recordedStatus = e.RecordedStatus.ToString(),
        e.PlanningEventId
    };
    internal static object Receipt(PlanningTaskReceipt r) => new
    { r.SchemaVersion, r.EventId, r.RunId, r.TaskId, kind = r.Kind.ToString(), r.Revision, r.ActorId, recordedAtUtc = r.RecordedAtUtc.UtcDateTime, r.SourceDigest };
}
