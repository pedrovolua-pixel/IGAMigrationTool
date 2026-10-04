namespace ReportPublication;

/// <summary>Stages immutable bytes and commits visibility only through reviewed trusted transaction ports.</summary>
public sealed class NativeReportPublisherV1(IPublicationAuthorityV1 authority, IPublicationSourceV1 source,
    IPublicationStoreV1 store, IImmutablePublicationBlobsV1 blobs, IPublicationOutcomeAuditV1 outcomeAudit)
{
    public async ValueTask<PublishResultV1> PublishAsync(PublicationActorV1 actor, PublishCommandV1 command, CancellationToken cancellationToken)
    {
        PublicationScopeV1? verifiedScope = null;
        var commitAttempted = false;
        try
        {
            if (!PublicationValidationV1.Command(actor, command)) return await Deny(PublicationIssueV1.InvalidInput, PublicationAuditReasonV1.InvalidInput);
            var attempt = await Execute();
            if (attempt.Issue is null) return attempt;
            return await Deny(attempt.Issue.Value, attempt.Issue switch
            {
                PublicationIssueV1.RevisionConflict => PublicationAuditReasonV1.RevisionConflict,
                PublicationIssueV1.IdempotencyConflict => PublicationAuditReasonV1.IdempotencyConflict,
                PublicationIssueV1.IntegrityMismatch => PublicationAuditReasonV1.IntegrityMismatch,
                PublicationIssueV1.DependencyUnavailable => PublicationAuditReasonV1.DependencyUnavailable,
                _ => verifiedScope is null ? PublicationAuditReasonV1.AuthorityDenied : PublicationAuditReasonV1.SourceUnavailable
            });
        }
        catch (PublicationCommitUncertainException)
        {
            outcomeAudit.OperationalSignal(PublicationOperationalSignalV1.CommitOutcomeUnknown, Safe(command?.InvocationId), Safe(command?.CorrelationId)); throw;
        }
        catch (Exception) when (commitAttempted)
        {
            outcomeAudit.OperationalSignal(PublicationOperationalSignalV1.CommitOutcomeUnknown, Safe(command?.InvocationId), Safe(command?.CorrelationId));
            throw new PublicationCommitUncertainException(Safe(command?.OperationId));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _ = await RecordOutcome(PublicationAuditOutcomeV1.Cancelled, PublicationAuditReasonV1.Cancelled); throw;
        }
        catch (Exception)
        { return await Deny(PublicationIssueV1.DependencyUnavailable, PublicationAuditReasonV1.DependencyUnavailable); }

        async ValueTask<PublishResultV1> Execute()
        {
            await using var fence = await authority.EnterPublishAsync(actor, command, cancellationToken);
            if (fence is null || fence.Actor != actor || fence.Scope != command.Scope) return new(PublicationIssueV1.Unavailable, null);
            verifiedScope = fence.Scope;
            await using var transaction = await store.BeginAsync(command.Scope, cancellationToken);
            if (transaction.Scope != command.Scope || transaction.TransactionId == Guid.Empty) return new(PublicationIssueV1.IntegrityMismatch, null);
            var commandDigest = NativePublicationCanonicalV1.Hash(NativePublicationCanonicalV1.CommandBytes(actor, command));
            var lookup = await transaction.ResolvePublicationReceiptAsync(command.OperationId, actor, commandDigest, cancellationToken);
            if (lookup.Status == ReceiptLookupStatusV1.Conflict) return new(PublicationIssueV1.IdempotencyConflict, null);
            if (lookup.Status == ReceiptLookupStatusV1.Found)
            {
                var receipt = lookup.Receipt;
                if (!PublicationValidationV1.PublicationReceipt(receipt) || receipt!.OperationId != command.OperationId || receipt.Actor != actor
                    || receipt.Scope != command.Scope || receipt.CommandDigest != commandDigest || receipt.RunId != command.RunId)
                    return new(PublicationIssueV1.IntegrityMismatch, null);
                var committed = await transaction.ReadCommittedVersionAsync(receipt.ReportVersionId, cancellationToken);
                if (committed is null || !PublicationValidationV1.Manifest(committed.Manifest) || committed.Manifest.Scope != command.Scope
                    || committed.ManifestDigest != receipt.ManifestDigest || committed.Manifest.ReportVersionId != receipt.ReportVersionId
                    || committed.Manifest.RunId != receipt.RunId || committed.Manifest.RunRevision != receipt.ExpectedRunRevision
                    || committed.Manifest.ProjectionDigest != receipt.ProjectionDigest || committed.Manifest.ScoreDigest != receipt.ScoreDigest
                    || NativePublicationCanonicalV1.Hash(NativePublicationCanonicalV1.ManifestBytes(committed.Manifest)) != receipt.ManifestDigest
                    || committed.LifecycleRevision <= 0 || committed.State != PublicationLifecycleStateV1.Active || committed.ReadBlocked
                    || committed.ExpiresAtUtc <= await transaction.ReadDatabaseUtcAsync(cancellationToken)
                    || committed.Manifest.Retention.ExpiresAtUtc <= await transaction.ReadDatabaseUtcAsync(cancellationToken)
                    || fence.OriginalDeadlineUtc <= await transaction.ReadDatabaseUtcAsync(cancellationToken)
                    || !await fence.RevalidateAsync(new(committed.Manifest.RequiredCategories, committed.Manifest.RequiredFields), cancellationToken))
                    return new(PublicationIssueV1.Unavailable, null);
                return new(null, receipt);
            }
            if (lookup.Status != ReceiptLookupStatusV1.NotFound || lookup.Receipt is not null) return new(PublicationIssueV1.IntegrityMismatch, null);
            var captured = await source.CaptureAsync(transaction, command, cancellationToken);
            if (!PublicationValidationV1.Source(captured)) return new(PublicationIssueV1.Unavailable, null);
            var p = captured!.Projection;
            if (p.Scope != command.Scope || p.RunId != command.RunId || p.RunRevision != command.ExpectedRunRevision) return new(PublicationIssueV1.RevisionConflict, null);
            var access = new RequiredPublicationAccessV1(captured.RequiredCategories, captured.RequiredFields);
            if (!await fence.RevalidateAsync(access, cancellationToken)) return new(PublicationIssueV1.Unavailable, null);
            var sourceDigest = NativePublicationCanonicalV1.Hash(NativePublicationCanonicalV1.SourceBytes(captured));
            if (sourceDigest != command.ExpectedSourceDigest) return new(PublicationIssueV1.RevisionConflict, null);
            if (!p.Warnings.ToHashSet().SetEquals(command.AcknowledgedWarnings)) return new(PublicationIssueV1.Unavailable, null);
            var createdAt = await transaction.ReadDatabaseUtcAsync(cancellationToken);
            if (createdAt.Offset != TimeSpan.Zero || captured.Retention.ExpiresAtUtc <= createdAt || fence.OriginalDeadlineUtc <= createdAt)
                return new(PublicationIssueV1.Unavailable, null);
            var projection = await Stage(PublicationBlobKindV1.Projection, NativePublicationCanonicalV1.ProjectionBytes(p));
            var score = await Stage(PublicationBlobKindV1.Score, NativePublicationCanonicalV1.ScoreBytes(p.Scores));
            var manifest = new PublicationManifestV1(p.Scope, Guid.NewGuid(), p.RunId, p.RunRevision, createdAt, new(actor.TenantId, actor.ObjectId),
                captured.AssessmentState, p.Warnings.IsEmpty ? PublicationApprovalV1.Published : PublicationApprovalV1.PublishedWithWarnings,
                p.Inputs, projection.Digest, score.Digest, sourceDigest, captured.RequiredCategories, captured.RequiredFields, p.RedactionMarkers,
                captured.Retention, captured.Provenance, [projection, score]);
            var manifestBlob = await Stage(PublicationBlobKindV1.Manifest, NativePublicationCanonicalV1.ManifestBytes(manifest));
            var audit = await transaction.AppendAuditAsync(new(command.OperationId, command.InvocationId, command.CorrelationId, actor, p.Scope,
                PublicationResourceKindV1.Run, p.RunId, PublicationAuditActionV1.Publish, PublicationAuditOutcomeV1.Succeeded,
                PublicationAuditReasonV1.None, manifestBlob.Digest, []), cancellationToken);
            if (!AuditCommitValid(audit)) return new(PublicationIssueV1.IntegrityMismatch, null);
            var result = new PublicationReceiptV1(command.OperationId, actor, p.Scope, p.RunId, p.RunRevision, commandDigest,
                manifest.ReportVersionId, manifestBlob.Digest, projection.Digest, score.Digest, audit.EventAtUtc, audit.EventId, audit.EventDigest);
            await transaction.AddPublicationAsync(new(manifest, manifestBlob.Digest, result), cancellationToken);
            var finalTime = await transaction.ReadDatabaseUtcAsync(cancellationToken);
            if (finalTime.Offset != TimeSpan.Zero || finalTime < createdAt || finalTime >= fence.OriginalDeadlineUtc || finalTime >= captured.Retention.ExpiresAtUtc
                || !await fence.RevalidateAsync(access, cancellationToken)
                || !await transaction.RevalidateSourceAsync(p.RunId, p.RunRevision, sourceDigest, cancellationToken))
                return new(PublicationIssueV1.Unavailable, null);
            cancellationToken.ThrowIfCancellationRequested();
            commitAttempted = true;
            try { await transaction.CommitAsync(cancellationToken); }
            catch (PublicationCommitNotAppliedException) { commitAttempted = false; return new(PublicationIssueV1.DependencyUnavailable, null); }
            catch (Exception) { throw new PublicationCommitUncertainException(command.OperationId); }
            return new(null, result);
        }

        async ValueTask<PublicationBlobDescriptorV1> Stage(PublicationBlobKindV1 kind, byte[] bytes)
        {
            var digest = NativePublicationCanonicalV1.Hash(bytes);
            var staged = await blobs.PutIfAbsentAsync(command.Scope, kind, bytes, cancellationToken);
            if (staged.Kind != kind || staged.Digest != digest || staged.ByteLength != bytes.LongLength) throw new InvalidOperationException("Invalid publication stage.");
            var read = await blobs.ReadVerifiedAsync(command.Scope, kind, digest, cancellationToken);
            if (read is null || !read.Value.Span.SequenceEqual(bytes) || NativePublicationCanonicalV1.Hash(read.Value.Span) != digest)
                throw new InvalidOperationException("Publication stage integrity failed.");
            return new(kind, digest, bytes.LongLength);
        }
        async ValueTask<PublishResultV1> Deny(PublicationIssueV1 issue, PublicationAuditReasonV1 reason)
            => new(await RecordOutcome(reason is PublicationAuditReasonV1.DependencyUnavailable or PublicationAuditReasonV1.IntegrityMismatch
                ? PublicationAuditOutcomeV1.Failed : PublicationAuditOutcomeV1.Denied, reason) ? issue : PublicationIssueV1.DependencyUnavailable, null);
        async ValueTask<bool> RecordOutcome(PublicationAuditOutcomeV1 outcome, PublicationAuditReasonV1 reason)
        {
            try
            {
                await outcomeAudit.RecordOutcomeAsync(new(Safe(command?.OperationId), Safe(command?.InvocationId), Safe(command?.CorrelationId),
                    verifiedScope is null ? null : actor, verifiedScope, verifiedScope is null ? null : PublicationResourceKindV1.Run,
                    verifiedScope is null ? null : command?.RunId, PublicationAuditActionV1.Publish, outcome, reason, null, []), cancellationToken);
                return true;
            }
            catch (Exception)
            { outcomeAudit.OperationalSignal(PublicationOperationalSignalV1.AuditUnavailable, Safe(command?.InvocationId), Safe(command?.CorrelationId)); return false; }
        }
    }
    internal static bool AuditCommitValid(CommittedPublicationAuditV1? audit) => audit is not null && audit.EventId != Guid.Empty
        && PublicationValidationV1.Digest(audit.EventDigest) && audit.EventAtUtc.Offset == TimeSpan.Zero;
    internal static Guid Safe(Guid? id) => id is { } value && value != Guid.Empty ? value : Guid.NewGuid();
}

/// <summary>Only a trusted store may use this when rollback/noncommit is positively established.</summary>
public sealed class PublicationCommitNotAppliedException : Exception
{
    public PublicationCommitNotAppliedException() : base("Publication transaction did not commit.") { }
}
