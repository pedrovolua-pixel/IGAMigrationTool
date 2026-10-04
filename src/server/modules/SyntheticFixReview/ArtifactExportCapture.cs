using System.Collections.Immutable;
using Npgsql;
using SyntheticSourceFence;

namespace SyntheticFixReview;

public sealed partial class SyntheticFixReviewStore
{
    /// <summary>Read-only owning API with genuine export authority; caller owns transaction, source validation and registry/run lock order.</summary>
    public async Task<ArtifactExportReadResult> ReadForExportInTransactionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid runId, ArtifactExportAuthority authority, ArtifactReviewSource source, CancellationToken cancellationToken = default)
    {
        if (runId == Guid.Empty || source is null) return new(ArtifactReviewIssue.InvalidInput, null);
        if (ArtifactExportPolicy.Authorize(authority, scope) is { } denied) return new(denied, null);
        var expected = new NpgsqlConnectionStringBuilder(connectionString); var actual = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
        if (transaction.Connection != connection || connection.State != System.Data.ConnectionState.Open || actual.Host != expected.Host ||
            actual.Port != expected.Port || actual.Database != expected.Database || actual.Username != expected.Username) return new(ArtifactReviewIssue.InvalidInput, null);
        foreach (var artifact in source.Artifacts)
            if (ArtifactExportPolicy.Authorize(authority, scope, artifact.CategoryId) is { } categoryDenied) return new(categoryDenied, null);
        await SyntheticRunSourceFence.AcquireAsync(connection, transaction, scope.CustomerId, scope.ProjectId, scope.EnvironmentId, runId, cancellationToken);
        if (await SyntheticFixReviewMigration.VerifyAsync(connection, transaction, scope, cancellationToken) is { } issue) return new(issue, null);
        try
        {
            var rebuilt = ArtifactReviewSourceBuilder.Build(source.Packages);
            if (!rebuilt.Succeeded || ArtifactReviewCanonical.Json(source.Binding) != ArtifactReviewCanonical.Json(rebuilt.Source!.Binding) ||
                ArtifactReviewCanonical.Json(source.Artifacts) != ArtifactReviewCanonical.Json(rebuilt.Source.Artifacts)) return new(ArtifactReviewIssue.IntegrityMismatch, null);
            if (source.Binding.Scope != scope) return new(ArtifactReviewIssue.WrongScope, null);
            if (source.Binding.RunId != runId) return new(ArtifactReviewIssue.SourceConflict, null);
            var entries = await ReadEntries(connection, transaction, rebuilt.Source, cancellationToken);
            var snapshot = new ArtifactReviewSnapshot(source.Binding, authority.ActorId, entries.Select(item => item.Entry).ToImmutableArray());
            var vectors = snapshot.Entries.Select(entry =>
            {
                var last = entry.History.IsDefaultOrEmpty ? null : entry.History[^1];
                return new ArtifactExportVector(entry.Artifact.ArtifactId, entry.Revision, last?.EventId, last?.Kind.ToString(), entry.State.ToString(), last?.Source.SourceDigest);
            }).OrderBy(item => item.ArtifactId, StringComparer.Ordinal).ToImmutableArray();
            var metadataDigest = ArtifactReviewCanonical.Digest(vectors);
            return new(null, new(metadataDigest, vectors, snapshot));
        }
        catch (Exception exception) when (IntegrityFailure(exception)) { return new(ArtifactReviewIssue.IntegrityMismatch, null); }
    }
}
