using System.Collections.Immutable;

namespace ReportPublication;

public interface IPublicationSourceV1
{
    ValueTask<SourceCaptureV1?> CaptureAsync(IPublicationTransactionV1 transaction,
        PublishCommandV1 command, CancellationToken cancellationToken);
}

public interface IPublicationAuthorityV1
{
    ValueTask<PublicationFenceV1?> EnterPublishAsync(PublicationActorV1 actor,
        PublishCommandV1 command, CancellationToken cancellationToken);
    ValueTask<PublicationFenceV1?> EnterExactReadAsync(PublicationActorV1 actor,
        ExactReportRequestV1 request, CancellationToken cancellationToken);
}

/// <summary>Trusted adapter-owned lease. It never mints a grant from an inline caller boolean.</summary>
public abstract class PublicationFenceV1 : IAsyncDisposable
{
    protected PublicationFenceV1(PublicationActorV1 actor, PublicationScopeV1 scope, DateTimeOffset originalDeadlineUtc)
    { Actor = actor; Scope = scope; OriginalDeadlineUtc = originalDeadlineUtc; }
    public PublicationActorV1 Actor { get; }
    public PublicationScopeV1 Scope { get; }
    public DateTimeOffset OriginalDeadlineUtc { get; }
    public abstract ValueTask<bool> RevalidateAsync(RequiredPublicationAccessV1 access, CancellationToken cancellationToken);
    public abstract ValueTask DisposeAsync();
}

public sealed class RequiredPublicationAccessV1
{
    public RequiredPublicationAccessV1(IEnumerable<string> categories, IEnumerable<string> fields)
    { Categories = OwnedValues.Copy(categories); Fields = OwnedValues.Copy(fields); }
    public ImmutableArray<string> Categories { get; }
    public ImmutableArray<string> Fields { get; }
}

public interface IImmutablePublicationBlobsV1
{
    ValueTask<StagedBlobV1> PutIfAbsentAsync(PublicationScopeV1 scope, PublicationBlobKindV1 kind,
        ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken);
    ValueTask<ReadOnlyMemory<byte>?> ReadVerifiedAsync(PublicationScopeV1 scope, PublicationBlobKindV1 kind,
        string expectedDigest, CancellationToken cancellationToken);
}

public sealed record StagedBlobV1(PublicationBlobKindV1 Kind, string Digest, long ByteLength);

/// <summary>Transaction lifetime and owning source seam; no public connection, SQL or provider locator.</summary>
public interface IPublicationTransactionV1 : IAsyncDisposable
{
    PublicationScopeV1 Scope { get; }
    Guid TransactionId { get; }
    ValueTask<DateTimeOffset> ReadDatabaseUtcAsync(CancellationToken cancellationToken);
    ValueTask<PublicationReceiptLookupV1> ResolvePublicationReceiptAsync(Guid operationId, PublicationActorV1 actor,
        string commandDigest, CancellationToken cancellationToken);
    ValueTask<ExactReadReceiptLookupV1> ResolveReadReceiptAsync(Guid invocationId, PublicationActorV1 actor,
        string requestDigest, CancellationToken cancellationToken);
    ValueTask<CommittedReportVersionV1?> ReadCommittedVersionAsync(Guid reportVersionId, CancellationToken cancellationToken);
    ValueTask<CommittedPublicationAuditV1> AppendAuditAsync(PublicationAuditIntentV1 intent, CancellationToken cancellationToken);
    ValueTask AddPublicationAsync(PublicationCommitV1 publication, CancellationToken cancellationToken);
    ValueTask AddReadReceiptAsync(ExactReadReceiptV1 receipt, CancellationToken cancellationToken);
    ValueTask<bool> RevalidateSourceAsync(Guid runId, long revision, string sourceDigest, CancellationToken cancellationToken);
    ValueTask CommitAsync(CancellationToken cancellationToken);
}

public interface IPublicationStoreV1
{
    ValueTask<IPublicationTransactionV1> BeginAsync(PublicationScopeV1 scope, CancellationToken cancellationToken);
}
public enum ReceiptLookupStatusV1 { NotFound, Found, Conflict }
public sealed record PublicationReceiptLookupV1(ReceiptLookupStatusV1 Status, PublicationReceiptV1? Receipt);
public sealed record ExactReadReceiptLookupV1(ReceiptLookupStatusV1 Status, ExactReadReceiptV1? Receipt);
public enum PublicationLifecycleStateV1 { Active, SoftDeleted, Expired }
public sealed record CommittedReportVersionV1(PublicationManifestV1 Manifest, string ManifestDigest,
    PublicationLifecycleStateV1 State, long LifecycleRevision, DateTimeOffset ExpiresAtUtc, bool ReadBlocked);
public sealed record PublicationCommitV1(PublicationManifestV1 Manifest, string ManifestDigest, PublicationReceiptV1 Receipt);
public sealed record CommittedPublicationAuditV1(Guid EventId, string EventDigest, DateTimeOffset EventAtUtc);

public sealed class PublicationAuditIntentV1
{
    public PublicationAuditIntentV1(Guid? operationId, Guid invocationId, Guid correlationId, PublicationActorV1? verifiedActor,
        PublicationScopeV1? verifiedScope, PublicationResourceKindV1? resourceKind, Guid? verifiedResourceId,
        PublicationAuditActionV1 action, PublicationAuditOutcomeV1 outcome, PublicationAuditReasonV1 reason,
        string? manifestDigest, IEnumerable<string> returnedFields)
    {
        OperationId = operationId; InvocationId = invocationId; CorrelationId = correlationId; VerifiedActor = verifiedActor;
        VerifiedScope = verifiedScope; ResourceKind = resourceKind; VerifiedResourceId = verifiedResourceId;
        Action = action; Outcome = outcome; Reason = reason; ManifestDigest = manifestDigest; ReturnedFields = OwnedValues.Copy(returnedFields);
    }
    public Guid? OperationId { get; }
    public Guid InvocationId { get; }
    public Guid CorrelationId { get; }
    public PublicationActorV1? VerifiedActor { get; }
    public PublicationScopeV1? VerifiedScope { get; }
    public PublicationResourceKindV1? ResourceKind { get; }
    public Guid? VerifiedResourceId { get; }
    public PublicationAuditActionV1 Action { get; }
    public PublicationAuditOutcomeV1 Outcome { get; }
    public PublicationAuditReasonV1 Reason { get; }
    public string? ManifestDigest { get; }
    public ImmutableArray<string> ReturnedFields { get; }
}
public interface IPublicationOutcomeAuditV1
{
    ValueTask RecordOutcomeAsync(PublicationAuditIntentV1 intent, CancellationToken cancellationToken);
    void OperationalSignal(PublicationOperationalSignalV1 signal, Guid invocationId, Guid correlationId);
}
public interface IReferenceAvailabilityV1
{
    ValueTask<IReadOnlyList<PublicationReferenceAvailabilityV1>> ReadAsync(IPublicationTransactionV1 transaction,
        IReadOnlyList<Guid> linkedReferenceIds, CancellationToken cancellationToken);
}
/// <summary>Configured trusted sink only; every bounded transport write must honor the original lease token.</summary>
public interface ITrustedReportDeliverySinkV1
{
    ValueTask DeliverAsync(ReadOnlyMemory<byte> canonicalProjection,
        IReadOnlyList<PublicationReferenceAvailabilityV1> currentReferenceAvailability, CancellationToken cancellationToken);
}

public sealed record PublicationReceiptV1(Guid OperationId, PublicationActorV1 Actor, PublicationScopeV1 Scope,
    Guid RunId, long ExpectedRunRevision, string CommandDigest, Guid ReportVersionId,
    string ManifestDigest, string ProjectionDigest, string ScoreDigest, DateTimeOffset CommittedAtUtc,
    Guid EventId, string EventDigest);
public sealed record ExactReadReceiptV1(Guid InvocationId, string RequestDigest, PublicationActorV1 Actor,
    PublicationScopeV1 Scope, Guid ReportVersionId, string ManifestDigest, DateTimeOffset CommittedAtUtc,
    Guid EventId, string EventDigest);
public sealed record PublishResultV1(PublicationIssueV1? Issue, PublicationReceiptV1? Receipt)
{
    public bool Succeeded => Issue is null && Receipt is not null;
}
public sealed record ExactReadResultV1(PublicationIssueV1? Issue, Guid? EventId);
public sealed record PublicationReferenceAvailabilityV1(Guid ReferenceId, PublicationAvailabilityV1 CurrentAvailability,
    PublicationReasonV1 Reason, long Revision);

/// <summary>No publicly accessible report buffer; only a trusted bounded reader may create a delivery view.</summary>
public abstract class VerifiedPublishedReportV1
{
    private protected VerifiedPublishedReportV1(Guid reportVersionId, string manifestDigest)
    { ReportVersionId = reportVersionId; ManifestDigest = manifestDigest; }
    public Guid ReportVersionId { get; }
    public string ManifestDigest { get; }
    public abstract ValueTask DeliverAsync(CancellationToken cancellationToken);
}

public sealed class PublicationCommitUncertainException : Exception
{
    public PublicationCommitUncertainException(Guid operationId) : base("Publication commit outcome is unknown.")
    { OperationId = operationId; }
    public Guid OperationId { get; }
}
