using System.Text.Json;
using AssessmentRuns;
using SyntheticFixReview;

/// <summary>Request-local source capture using owning modules; no client source or shared mutable capture.</summary>
internal sealed class DemoArtifactReviewService(string connection, SyntheticDurableRunEngine engine, DemoReviewService reviews)
{
    internal static ArtifactReviewAuthority Authority { get; } = new("synthetic-consultant", true, true, false, true,
        ArtifactReviewScope.Fixed, [ArtifactReviewRole.Consultant], ["SECURITY", "OPERATIONS"],
        [ArtifactReviewAction.Read, ArtifactReviewAction.Review], ArtifactReviewResourceState.Mutable);

    internal Task InitializeAsync(CancellationToken cancellationToken = default) =>
        new SyntheticFixReviewStore(connection, ArtifactReviewScope.Fixed,
            (_, _) => Task.FromResult(new ArtifactReviewSourceResult(ArtifactReviewIssue.SourceUnavailable, null)))
            .InitializeAsync(cancellationToken);

    internal async Task<object?> ReadAnalysisAsync(SyntheticRunSnapshot initial, CancellationToken cancellationToken = default)
    {
        // Initial immutable finding seed is committed outside the artifact source fence.
        var seeded = await reviews.ReadAsync(initial, cancellationToken);
        DemoAnalysisCapture? capture = null;
        var store = Store(value => capture = value);
        var read = seeded.Snapshot is null
            ? new ArtifactReviewReadResult(ArtifactReviewIssue.SourceUnavailable, null)
            : await store.ReadAsync(initial.RunId, Authority, cancellationToken);
        // A pre-capture denial must not present the earlier, unfenced source as current.
        if (capture is null) return null;
        capture.Analysis["artifactReview"] = JsonSerializer.SerializeToNode(Detail(read), DemoReportDraftProjection.JsonOptions);
        return capture.Analysis;
    }

    internal async Task<ArtifactReviewApplyResult> ApplyAsync(Guid runId, string artifactId,
        ArtifactReviewCommand command, CancellationToken cancellationToken = default)
    {
        if (runId == Guid.Empty || !ArtifactReviewPolicy.ValidDigest(artifactId) || ArtifactReviewPolicy.ValidateCommand(command) is not null)
            return new(ArtifactReviewIssue.InvalidInput, null);
        var current = await engine.ReadAsync(DemoFixtureCatalog.Scope, runId, cancellationToken);
        if (!current.Succeeded || !DemoArtifactReviewCatalog.MatchesFrozenFixture(current.Snapshot))
            return new(ArtifactReviewIssue.SourceUnavailable, null);
        var seeded = await reviews.ReadExistingAsync(current.Snapshot!, cancellationToken);
        if (seeded.Snapshot is null) return new(ArtifactReviewIssue.SourceUnavailable, null);
        return await Store(_ => { }).ApplyAsync(runId, artifactId, Authority, command, cancellationToken);
    }

    private SyntheticFixReviewStore Store(Action<DemoAnalysisCapture> captured) => new(connection,
        ArtifactReviewScope.Fixed, async (runId, cancellationToken) =>
        {
            var read = await engine.ReadAsync(DemoFixtureCatalog.Scope, runId, cancellationToken);
            if (!read.Succeeded || !DemoArtifactReviewCatalog.MatchesFrozenFixture(read.Snapshot))
                return new(ArtifactReviewIssue.SourceUnavailable, null);
            var review = await reviews.ReadExistingAsync(read.Snapshot!, cancellationToken);
            var value = DemoAnalysisProjection.Capture(read.Snapshot!, review);
            captured(value);
            return ArtifactReviewSourceBuilder.Build(value.FixPackages?.Snapshot);
        });

    internal static object Detail(ArtifactReviewReadResult read) => new
    {
        schemaVersion = 1,
        demoOnly = true,
        status = read.Succeeded ? "Ready" : "Unavailable",
        reasonCode = read.Succeeded ? null : read.Issue switch
        {
            ArtifactReviewIssue.Denied or ArtifactReviewIssue.WrongScope => "artifact_review_denied",
            ArtifactReviewIssue.IntegrityMismatch or ArtifactReviewIssue.MigrationDrift or ArtifactReviewIssue.SeedConflict => "artifact_review_integrity_denied",
            _ => "artifact_review_source_unavailable"
        },
        source = read.Snapshot?.Source,
        actorId = read.Snapshot?.ActorId,
        artifacts = read.Snapshot is null ? [] : read.Snapshot.Entries.OrderBy(entry => entry.Artifact.ArtifactId, StringComparer.Ordinal).Select(entry => new
        {
            entry.Artifact.FindingId,
            entry.Artifact.CategoryId,
            entry.Artifact.PackageId,
            entry.Artifact.ScopedOptionId,
            entry.Artifact.ArtifactId,
            entry.Artifact.TemplateId,
            entry.Artifact.Kind,
            entry.Artifact.ArtifactTextDigest,
            entry.Revision,
            state = entry.State.ToString(),
            entry.CanReview,
            entry.CanWithdraw,
            history = entry.History.Select(item => new
            {
                item.EventId,
                item.Revision,
                kind = item.Kind.ToString(),
                item.ActorId,
                item.ActorRoles,
                recordedAtUtc = item.RecordedAtUtc.UtcDateTime,
                item.Reason,
                item.Source,
                recordedState = item.RecordedState.ToString()
            }).ToArray()
        }).ToArray()
    };

    internal static object Receipt(ArtifactReviewReceipt receipt) => new
    {
        receipt.SchemaVersion,
        receipt.EventId,
        receipt.RunId,
        receipt.ArtifactId,
        kind = receipt.Kind.ToString(),
        receipt.Revision,
        receipt.ActorId,
        recordedAtUtc = receipt.RecordedAtUtc.UtcDateTime,
        receipt.SourceDigest
    };
}
