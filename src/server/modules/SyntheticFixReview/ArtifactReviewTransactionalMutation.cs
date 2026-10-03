using Npgsql;
using SyntheticSourceFence;

namespace SyntheticFixReview;

public sealed partial class SyntheticFixReviewStore
{
    public async Task<ArtifactReviewApplyResult> ApplyInTransactionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid runId, string artifactId, ArtifactReviewAuthority authority,
        ArtifactReviewCommand command, ArtifactReviewSource source, CancellationToken cancellationToken = default)
    {
        if (runId == Guid.Empty || !ArtifactReviewPolicy.ValidDigest(artifactId) || ArtifactReviewPolicy.ValidateCommand(command) is not null)
            return new(ArtifactReviewIssue.InvalidInput, null);
        if (ArtifactReviewPolicy.Authorize(authority, scope) is { } denied) return new(denied, null);
        var expected = new NpgsqlConnectionStringBuilder(connectionString); var actual = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
        if (transaction.Connection != connection || connection.State != System.Data.ConnectionState.Open || actual.Host != expected.Host ||
            actual.Port != expected.Port || actual.Database != expected.Database || actual.Username != expected.Username) return new(ArtifactReviewIssue.InvalidInput, null);
        await SyntheticRunSourceFence.AcquireAsync(connection, transaction, scope.CustomerId, scope.ProjectId, scope.EnvironmentId, runId, cancellationToken);
        if (await SyntheticFixReviewMigration.VerifyAsync(connection, transaction, scope, cancellationToken) is { } issue) return new(issue, null);
        try
        {
            var rebuilt = ArtifactReviewSourceBuilder.Build(source.Packages);
            if (!rebuilt.Succeeded || ArtifactReviewCanonical.Json(source.Binding) != ArtifactReviewCanonical.Json(rebuilt.Source!.Binding) ||
                ArtifactReviewCanonical.Json(source.Artifacts) != ArtifactReviewCanonical.Json(rebuilt.Source.Artifacts)) return new(ArtifactReviewIssue.IntegrityMismatch, null);
            if (source.Binding.Scope != scope) return new(ArtifactReviewIssue.WrongScope, null);
            if (source.Binding.RunId != runId) return new(ArtifactReviewIssue.SourceConflict, null);
            foreach (var finding in source.Packages.Guidance.Findings)
                if (ArtifactReviewPolicy.Authorize(authority, scope, finding.CategoryId) is { } categoryDenied) return new(categoryDenied, null);
            if (!source.Artifacts.Any(item => item.ArtifactId == artifactId)) return new(ArtifactReviewIssue.NotFound, null);
            // Validate complete visible source and every artifact history before replay lookup.
            var entries = await ReadEntries(connection, transaction, source, cancellationToken);
            var entry = entries.Single(item => item.Entry.Artifact.ArtifactId == artifactId);
            var payloadDigest = ArtifactReviewCanonical.CommandDigest(scope, runId, artifactId, authority.ActorId, command);
            var replay = entry.Events.SingleOrDefault(item => item.Event.EventId == command.EventId);
            if (replay is not null)
                return replay.CommandDigest == payloadDigest ? new(null, replay.Receipt, AlreadyApplied: true) : new(ArtifactReviewIssue.EventConflict, null);
            if (command.ExpectedRevision != entry.Entry.Revision) return new(ArtifactReviewIssue.RevisionConflict, null);
            if (command.ExpectedSourceDigest != source.Binding.SourceDigest) return new(ArtifactReviewIssue.SourceConflict, null);
            if (ArtifactReviewPolicy.Transition(entry.Entry.State, command.Kind) is { } transition) return new(transition, null);
            if (entry.Entry.Revision >= ArtifactReviewPolicy.MaximumRevision) return new(ArtifactReviewIssue.RevisionOverflow, null);
            var revision = checked(entry.Entry.Revision + 1);
            await Register(connection, transaction, source, entry.Entry.Artifact, entry.Events.IsEmpty, cancellationToken);
            await using var clock = new NpgsqlCommand("SELECT clock_timestamp()", connection, transaction);
            var now = (DateTime)(await clock.ExecuteScalarAsync(cancellationToken))!;
            var historyEvent = new ArtifactReviewEvent(command.EventId, revision, command.Kind, authority.ActorId, ["Consultant"], new(now), command.Reason,
                source.Binding, command.Kind == ArtifactReviewKind.ReviewForPlanning ? ArtifactReviewState.ReviewedForPlanning : ArtifactReviewState.Unverified);
            var current = Current(historyEvent);
            var receipt = Receipt(runId, artifactId, historyEvent);
            await Execute(connection, transaction, """
                INSERT INTO synthetic_fix_review.events VALUES (@run,@artifact,@event,@expected,@revision,@source,@command,@json,@digest,@after,@time)
                """, cancellationToken, ("run", runId), ("artifact", artifactId), ("event", command.EventId), ("expected", command.ExpectedRevision),
                ("revision", revision), ("source", source.Binding.SourceDigest), ("command", payloadDigest), ("json", ArtifactReviewCanonical.Json(historyEvent)),
                ("digest", ArtifactReviewCanonical.Digest(historyEvent)), ("after", ArtifactReviewCanonical.Json(current)), ("time", now));
            await Execute(connection, transaction, "INSERT INTO synthetic_fix_review.receipts VALUES (@run,@artifact,@event,@json,@digest)", cancellationToken,
                ("run", runId), ("artifact", artifactId), ("event", command.EventId), ("json", ArtifactReviewCanonical.Json(receipt)), ("digest", ArtifactReviewCanonical.Digest(receipt)));
            await Execute(connection, transaction, "UPDATE synthetic_fix_review.artifact_current SET revision=@revision,current_json=@json WHERE run_id=@run AND artifact_id=@artifact", cancellationToken,
                ("revision", revision), ("json", ArtifactReviewCanonical.Json(current)), ("run", runId), ("artifact", artifactId));
            if (observer is not null) await observer.BeforeCommitAsync("apply", runId, artifactId, command.EventId, cancellationToken);
            return new(null, receipt);
        }
        catch (Exception exception) when (IntegrityFailure(exception)) { return new(ArtifactReviewIssue.IntegrityMismatch, null); }
    }
}
