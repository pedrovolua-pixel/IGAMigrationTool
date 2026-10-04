using System.Security.Cryptography;

namespace ReportPublication;

/// <summary>Exact owning read with durable audit before lease-bound protected delivery.</summary>
public sealed class NativePublishedReportReaderV1(IPublicationAuthorityV1 authority, IPublicationStoreV1 store,
    IImmutablePublicationBlobsV1 blobs, IReferenceAvailabilityV1 references, ITrustedReportDeliverySinkV1 sink,
    IPublicationOutcomeAuditV1 outcomeAudit, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public async ValueTask<ExactReadResultV1> ReadExactAsync(PublicationActorV1 actor, ExactReportRequestV1 request,
        Func<VerifiedPublishedReportV1, CancellationToken, ValueTask> emit, CancellationToken cancellationToken)
    {
        PublicationScopeV1? verifiedScope = null;
        var commitAttempted = false;
        var committed = false;
        try
        {
            if (!PublicationValidationV1.ExactRequest(actor, request) || emit is null) return await Deny(PublicationIssueV1.InvalidInput, PublicationAuditReasonV1.InvalidInput);
            var result = await Execute();
            if (result.Issue is null || committed) return result;
            return await Deny(result.Issue.Value, result.Issue switch
            {
                PublicationIssueV1.IdempotencyConflict => PublicationAuditReasonV1.IdempotencyConflict,
                PublicationIssueV1.IntegrityMismatch => PublicationAuditReasonV1.IntegrityMismatch,
                PublicationIssueV1.DependencyUnavailable => PublicationAuditReasonV1.DependencyUnavailable,
                _ => verifiedScope is null ? PublicationAuditReasonV1.AuthorityDenied : PublicationAuditReasonV1.LifecycleDenied
            });
        }
        catch (PublicationCommitUncertainException)
        {
            outcomeAudit.OperationalSignal(PublicationOperationalSignalV1.CommitOutcomeUnknown, NativeReportPublisherV1.Safe(request?.InvocationId), NativeReportPublisherV1.Safe(request?.CorrelationId));
            throw;
        }
        catch (Exception) when (commitAttempted && !committed)
        {
            outcomeAudit.OperationalSignal(PublicationOperationalSignalV1.CommitOutcomeUnknown, NativeReportPublisherV1.Safe(request?.InvocationId), NativeReportPublisherV1.Safe(request?.CorrelationId));
            throw new PublicationCommitUncertainException(NativeReportPublisherV1.Safe(request?.InvocationId));
        }
        catch (PublicationIntegrityException)
        { return committed ? new(PublicationIssueV1.IntegrityMismatch, null) : await Deny(PublicationIssueV1.IntegrityMismatch, PublicationAuditReasonV1.IntegrityMismatch); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (!committed) _ = await RecordOutcome(PublicationAuditOutcomeV1.Cancelled, PublicationAuditReasonV1.Cancelled);
            throw;
        }
        catch (OperationCanceledException)
        { return committed ? new(PublicationIssueV1.Unavailable, null) : await Deny(PublicationIssueV1.Unavailable, PublicationAuditReasonV1.LifecycleDenied); }
        catch (Exception)
        { return committed ? new(PublicationIssueV1.Unavailable, null) : await Deny(PublicationIssueV1.DependencyUnavailable, PublicationAuditReasonV1.DependencyUnavailable); }

        async ValueTask<ExactReadResultV1> Execute()
        {
            await using var fence = await authority.EnterExactReadAsync(actor, request, cancellationToken);
            if (fence is null || fence.Actor != actor || fence.Scope != request.Scope) return new(PublicationIssueV1.Unavailable, null);
            verifiedScope = fence.Scope;
            var admittedUtc = clock.GetUtcNow();
            var admittedTimestamp = clock.GetTimestamp();
            using var lease = new EmissionLease(clock, admittedUtc, admittedTimestamp, fence.OriginalDeadlineUtc, cancellationToken);
            lease.Check();
            await using var transaction = await store.BeginAsync(request.Scope, lease.Token);
            if (transaction.Scope != request.Scope || transaction.TransactionId == Guid.Empty) return new(PublicationIssueV1.IntegrityMismatch, null);
            var version = await transaction.ReadCommittedVersionAsync(request.ReportVersionId, lease.Token);
            var readStartedAt = await transaction.ReadDatabaseUtcAsync(lease.Token);
            if (!ValidVersion(version, request, readStartedAt)) return new(PublicationIssueV1.Unavailable, null);
            var m = version!.Manifest;
            var deadline = fence.OriginalDeadlineUtc < version.ExpiresAtUtc ? fence.OriginalDeadlineUtc : version.ExpiresAtUtc;
            if (deadline > m.Retention.ExpiresAtUtc) deadline = m.Retention.ExpiresAtUtc;
            lease.Constrain(deadline);
            lease.Check();
            var access = new RequiredPublicationAccessV1(m.RequiredCategories, m.RequiredFields);
            if (!await fence.RevalidateAsync(access, lease.Token)) return new(PublicationIssueV1.Unavailable, null);
            var requestDigest = NativePublicationCanonicalV1.Hash(NativePublicationCanonicalV1.ReadRequestBytes(actor, request));
            var lookup = await transaction.ResolveReadReceiptAsync(request.InvocationId, actor, requestDigest, lease.Token);
            if (lookup.Status == ReceiptLookupStatusV1.Conflict) return new(PublicationIssueV1.IdempotencyConflict, null);
            if (lookup.Status == ReceiptLookupStatusV1.Found && (!PublicationValidationV1.ReadReceipt(lookup.Receipt)
                || lookup.Receipt!.Actor != actor || lookup.Receipt.Scope != request.Scope || lookup.Receipt.InvocationId != request.InvocationId
                || lookup.Receipt.RequestDigest != requestDigest || lookup.Receipt.ReportVersionId != request.ReportVersionId
                || lookup.Receipt.ManifestDigest != request.ExpectedManifestDigest)) return new(PublicationIssueV1.IntegrityMismatch, null);
            if (lookup.Status == ReceiptLookupStatusV1.NotFound && lookup.Receipt is not null
                || !Enum.IsDefined(lookup.Status)) return new(PublicationIssueV1.IntegrityMismatch, null);
            lease.Check();
            var manifestBytes = NativePublicationCanonicalV1.ManifestBytes(m);
            if (NativePublicationCanonicalV1.Hash(manifestBytes) != request.ExpectedManifestDigest
                || !await MatchesBlob(PublicationBlobKindV1.Manifest, request.ExpectedManifestDigest, manifestBytes, lease)) return new(PublicationIssueV1.IntegrityMismatch, null);
            lease.Check();
            var original = await blobs.ReadVerifiedAsync(request.Scope, PublicationBlobKindV1.Projection, m.ProjectionDigest, lease.Token);
            lease.Check();
            if (original is null) return new(PublicationIssueV1.IntegrityMismatch, null);
            var owned = original.Value.ToArray();
            try
            {
                var projectionDescriptor = m.ArtifactInputs.Single(a => a.Kind == PublicationBlobKindV1.Projection);
                if (owned.LongLength != projectionDescriptor.ByteLength || NativePublicationCanonicalV1.Hash(owned) != m.ProjectionDigest
                    || !NativePublicationProjectionReaderV1.TryRead(owned, out var p) || p!.Scope != m.Scope || p.RunId != m.RunId
                    || p.RunRevision != m.RunRevision || p.Inputs != m.Inputs
                    || !p.RedactionMarkers.ToHashSet().SetEquals(m.RedactionMarkers)
                    || m.ApprovalState != (p.Warnings.IsEmpty ? PublicationApprovalV1.Published : PublicationApprovalV1.PublishedWithWarnings)) return new(PublicationIssueV1.IntegrityMismatch, null);
                var scoreBytes = NativePublicationCanonicalV1.ScoreBytes(p.Scores);
                if (m.ArtifactInputs.Single(a => a.Kind == PublicationBlobKindV1.Score).ByteLength != scoreBytes.LongLength
                    || NativePublicationCanonicalV1.Hash(scoreBytes) != m.ScoreDigest
                    || !await MatchesBlob(PublicationBlobKindV1.Score, m.ScoreDigest, scoreBytes, lease)) return new(PublicationIssueV1.IntegrityMismatch, null);
                var source = new SourceCaptureV1(p, m.AssessmentState, m.Retention, m.RequiredCategories, m.RequiredFields, m.Provenance);
                if (!PublicationValidationV1.Source(source) || NativePublicationCanonicalV1.Hash(NativePublicationCanonicalV1.SourceBytes(source)) != m.SourceDigest)
                    return new(PublicationIssueV1.IntegrityMismatch, null);
                var linked = p.TechnicalAppendices.ProtectedReferences.Select(r => r.Id).Order().ToArray();
                lease.Check();
                var overlay = await references.ReadAsync(transaction, linked, lease.Token);
                lease.Check();
                if (!ValidOverlay(linked, overlay)) return new(PublicationIssueV1.IntegrityMismatch, null);
                lease.Check();
                if (!await fence.RevalidateAsync(access, lease.Token)
                    || !ValidVersion(await transaction.ReadCommittedVersionAsync(request.ReportVersionId, lease.Token), request, await transaction.ReadDatabaseUtcAsync(lease.Token)))
                    return new(PublicationIssueV1.Unavailable, null);
                Guid eventId;
                if (lookup.Status == ReceiptLookupStatusV1.Found) eventId = lookup.Receipt!.EventId;
                else
                {
                    var audit = await transaction.AppendAuditAsync(new(null, request.InvocationId, request.CorrelationId, actor, request.Scope,
                        PublicationResourceKindV1.ReportVersion, request.ReportVersionId, PublicationAuditActionV1.ReadExact,
                        PublicationAuditOutcomeV1.Succeeded, PublicationAuditReasonV1.None, request.ExpectedManifestDigest, m.RequiredFields), lease.Token);
                    if (!NativeReportPublisherV1.AuditCommitValid(audit) || audit.EventAtUtc < readStartedAt) return new(PublicationIssueV1.IntegrityMismatch, null);
                    await transaction.AddReadReceiptAsync(new(request.InvocationId, requestDigest, actor, request.Scope, request.ReportVersionId,
                        request.ExpectedManifestDigest, audit.EventAtUtc, audit.EventId, audit.EventDigest), lease.Token);
                    lease.Check();
                    var finalAuditTime = await transaction.ReadDatabaseUtcAsync(lease.Token);
                    if (finalAuditTime.Offset != TimeSpan.Zero || audit.EventAtUtc > finalAuditTime) return new(PublicationIssueV1.IntegrityMismatch, null);
                    if (!await fence.RevalidateAsync(access, lease.Token)
                        || !ValidVersion(await transaction.ReadCommittedVersionAsync(request.ReportVersionId, lease.Token), request,
                            await transaction.ReadDatabaseUtcAsync(lease.Token))) return new(PublicationIssueV1.Unavailable, null);
                    commitAttempted = true;
                    try { await transaction.CommitAsync(lease.Token); }
                    catch (PublicationCommitNotAppliedException) { commitAttempted = false; return new(PublicationIssueV1.DependencyUnavailable, null); }
                    committed = true;
                    eventId = audit.EventId;
                }
                // An existing committed receipt is equally irreversible; callback errors cannot rewrite its audit outcome.
                committed = true;
                var view = new DeliveryView(request.ReportVersionId, request.ExpectedManifestDigest, owned, lease, async token =>
                {
                    if (!await fence.RevalidateAsync(access, token)
                        || !ValidVersion(await transaction.ReadCommittedVersionAsync(request.ReportVersionId, token), request, await transaction.ReadDatabaseUtcAsync(token)))
                        throw new OperationCanceledException(token);
                    var supplied = await references.ReadAsync(transaction, linked, token);
                    if (supplied is null) throw new PublicationIntegrityException();
                    var current = OwnedValues.Copy(supplied);
                    if (!ValidOverlay(linked, current)) throw new PublicationIntegrityException();
                    return current;
                }, sink);
                try
                {
                    await emit(view, lease.Token).AsTask().WaitAsync(lease.Token);
                    lease.Check();
                    return new(null, eventId);
                }
                finally { view.Invalidate(); }
            }
            finally { CryptographicOperations.ZeroMemory(owned); }
        }

        async ValueTask<bool> MatchesBlob(PublicationBlobKindV1 kind, string digest, byte[] expected, EmissionLease lease)
        {
            lease.Check();
            var loaded = await blobs.ReadVerifiedAsync(request.Scope, kind, digest, lease.Token);
            lease.Check();
            return loaded is not null && loaded.Value.Span.SequenceEqual(expected) && NativePublicationCanonicalV1.Hash(loaded.Value.Span) == digest;
        }
        async ValueTask<ExactReadResultV1> Deny(PublicationIssueV1 issue, PublicationAuditReasonV1 reason)
            => new(await RecordOutcome(reason is PublicationAuditReasonV1.DependencyUnavailable or PublicationAuditReasonV1.IntegrityMismatch
                ? PublicationAuditOutcomeV1.Failed : PublicationAuditOutcomeV1.Denied, reason) ? issue : PublicationIssueV1.DependencyUnavailable, null);
        async ValueTask<bool> RecordOutcome(PublicationAuditOutcomeV1 outcome, PublicationAuditReasonV1 reason)
        {
            try
            {
                await outcomeAudit.RecordOutcomeAsync(new(null, NativeReportPublisherV1.Safe(request?.InvocationId), NativeReportPublisherV1.Safe(request?.CorrelationId),
                    verifiedScope is null ? null : actor, verifiedScope, verifiedScope is null ? null : PublicationResourceKindV1.ReportVersion,
                    verifiedScope is null ? null : request?.ReportVersionId, PublicationAuditActionV1.ReadExact, outcome, reason, null, []), cancellationToken);
                return true;
            }
            catch (Exception)
            { outcomeAudit.OperationalSignal(PublicationOperationalSignalV1.AuditUnavailable, NativeReportPublisherV1.Safe(request?.InvocationId), NativeReportPublisherV1.Safe(request?.CorrelationId)); return false; }
        }
    }

    private static bool ValidVersion(CommittedReportVersionV1? v, ExactReportRequestV1 request, DateTimeOffset now)
        => v is not null && PublicationValidationV1.Manifest(v.Manifest) && v.Manifest.Scope == request.Scope
        && v.Manifest.ReportVersionId == request.ReportVersionId && v.ManifestDigest == request.ExpectedManifestDigest
        && v.LifecycleRevision > 0 && v.State == PublicationLifecycleStateV1.Active && !v.ReadBlocked && now.Offset == TimeSpan.Zero
        && now < v.ExpiresAtUtc && now < v.Manifest.Retention.ExpiresAtUtc;
    private static bool ValidOverlay(Guid[] linked, IReadOnlyList<PublicationReferenceAvailabilityV1>? rows)
        => rows is not null && rows.Count == linked.Length && rows.All(r => r is not null) && rows.Select(r => r.ReferenceId).ToHashSet().SetEquals(linked)
        && rows.All(r => r is not null && r.Revision > 0 && Enum.IsDefined(r.CurrentAvailability) && Enum.IsDefined(r.Reason)
            && (r.CurrentAvailability == PublicationAvailabilityV1.Available ? r.Reason == PublicationReasonV1.None : r.Reason != PublicationReasonV1.None));

    private sealed class EmissionLease : IDisposable
    {
        private readonly TimeProvider clock;
        private readonly long started;
        private readonly DateTimeOffset admittedUtc;
        private TimeSpan maximumElapsed;
        private DateTimeOffset deadline;
        private readonly CancellationTokenSource expiry;
        private readonly CancellationTokenSource linked;
        private bool active = true;
        internal EmissionLease(TimeProvider clock, DateTimeOffset admittedUtc, long started, DateTimeOffset deadline, CancellationToken caller)
        {
            this.clock = clock; this.started = started; this.deadline = deadline; this.admittedUtc = admittedUtc;
            maximumElapsed = deadline - admittedUtc;
            var remainingUtc = deadline - clock.GetUtcNow();
            var remainingElapsed = maximumElapsed - clock.GetElapsedTime(started);
            var remaining = remainingUtc < remainingElapsed ? remainingUtc : remainingElapsed;
            expiry = new(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero, clock);
            linked = CancellationTokenSource.CreateLinkedTokenSource(caller, expiry.Token);
        }
        internal CancellationToken Token => linked.Token;
        internal void Constrain(DateTimeOffset earlierDeadline)
        {
            Check();
            if (earlierDeadline >= deadline) return;
            deadline = earlierDeadline;
            maximumElapsed = deadline - admittedUtc;
            var remainingUtc = deadline - clock.GetUtcNow();
            var remainingElapsed = maximumElapsed - clock.GetElapsedTime(started);
            var remaining = remainingUtc < remainingElapsed ? remainingUtc : remainingElapsed;
            expiry.CancelAfter(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero);
            Check();
        }
        internal void Check()
        {
            if (!active || clock.GetUtcNow() >= deadline || clock.GetElapsedTime(started) >= maximumElapsed)
                throw new OperationCanceledException(Token);
            Token.ThrowIfCancellationRequested();
        }
        internal void Invalidate() { active = false; linked.Cancel(); }
        public void Dispose() { Invalidate(); linked.Dispose(); expiry.Dispose(); }
    }
    private sealed class DeliveryView(Guid id, string digest, byte[] bytes, EmissionLease lease,
        Func<CancellationToken, ValueTask<IReadOnlyList<PublicationReferenceAvailabilityV1>>> revalidate, ITrustedReportDeliverySinkV1 sink)
        : VerifiedPublishedReportV1(id, digest)
    {
        private int active = 1;
        private readonly SemaphoreSlim serial = new(1, 1);
        internal void Invalidate() { Interlocked.Exchange(ref active, 0); lease.Invalidate(); }
        public override async ValueTask DeliverAsync(CancellationToken cancellationToken)
        {
            using var combined = CancellationTokenSource.CreateLinkedTokenSource(lease.Token, cancellationToken);
            await serial.WaitAsync(combined.Token);
            try
            {
                Check();
                var overlay = await revalidate(combined.Token);
                Check();
                await sink.DeliverAsync(bytes, overlay, combined.Token);
                Check();
            }
            finally { serial.Release(); }
            void Check() { if (Volatile.Read(ref active) == 0) throw new OperationCanceledException(combined.Token); lease.Check(); }
        }
    }
}
