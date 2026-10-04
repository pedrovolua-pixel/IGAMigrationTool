using System.Collections.Immutable;

namespace ReportPublication;

public sealed record PublicationCreatorV1(Guid TenantId, Guid ObjectId);

public sealed class PublicationManifestV1
{
    public PublicationManifestV1(PublicationScopeV1 scope, Guid reportVersionId, Guid runId, long runRevision,
        DateTimeOffset createdAtUtc, PublicationCreatorV1 createdBy, AssessmentStateV1 assessmentState,
        PublicationApprovalV1 approvalState, PublicationInputsV1 inputs, string projectionDigest, string scoreDigest,
        string sourceDigest, IEnumerable<string> requiredCategories, IEnumerable<string> requiredFields,
        IEnumerable<PublicationRedactionMarkerV1> redactionMarkers, PublicationRetentionV1 retention,
        IEnumerable<PublicationProvenanceV1> provenance, IEnumerable<PublicationBlobDescriptorV1> artifactInputs)
    {
        Scope = scope; ReportVersionId = reportVersionId; RunId = runId; RunRevision = runRevision;
        CreatedAtUtc = createdAtUtc; CreatedBy = createdBy; AssessmentState = assessmentState; ApprovalState = approvalState;
        Inputs = inputs; ProjectionDigest = projectionDigest; ScoreDigest = scoreDigest; SourceDigest = sourceDigest;
        RequiredCategories = OwnedValues.Copy(requiredCategories); RequiredFields = OwnedValues.Copy(requiredFields);
        RedactionMarkers = OwnedValues.Copy(redactionMarkers); Retention = retention;
        Provenance = OwnedValues.Copy(provenance); ArtifactInputs = OwnedValues.Copy(artifactInputs);
    }
    public PublicationScopeV1 Scope { get; }
    public Guid ReportVersionId { get; }
    public Guid RunId { get; }
    public long RunRevision { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public PublicationCreatorV1 CreatedBy { get; }
    public AssessmentStateV1 AssessmentState { get; }
    public PublicationApprovalV1 ApprovalState { get; }
    public PublicationInputsV1 Inputs { get; }
    public string ProjectionDigest { get; }
    public string ScoreDigest { get; }
    public string SourceDigest { get; }
    public ImmutableArray<string> RequiredCategories { get; }
    public ImmutableArray<string> RequiredFields { get; }
    public ImmutableArray<PublicationRedactionMarkerV1> RedactionMarkers { get; }
    public PublicationRetentionV1 Retention { get; }
    public ImmutableArray<PublicationProvenanceV1> Provenance { get; }
    public ImmutableArray<PublicationBlobDescriptorV1> ArtifactInputs { get; }
}

public sealed class PublicationAuditEventV1
{
    public PublicationAuditEventV1(Guid eventId, Guid streamId, Guid writerBindingReference, long sequence,
        string previousEventDigest, DateTimeOffset eventAtUtc, Guid? operationId, Guid invocationId, Guid correlationId,
        PublicationAuditActorKindV1 actorKind, PublicationActorV1? actor, PublicationScopeV1? scope,
        PublicationResourceKindV1? resourceKind, Guid? resourceId, PublicationAuditActionV1 action,
        PublicationAuditOutcomeV1 outcome, PublicationAuditReasonV1 reason, string? manifestDigest,
        IEnumerable<string> returnedFields, IEnumerable<string> redactedFields)
    {
        EventId = eventId; StreamId = streamId; WriterBindingReference = writerBindingReference; Sequence = sequence;
        PreviousEventDigest = previousEventDigest; EventAtUtc = eventAtUtc; OperationId = operationId; InvocationId = invocationId;
        CorrelationId = correlationId; ActorKind = actorKind; Actor = actor; Scope = scope; ResourceKind = resourceKind;
        ResourceId = resourceId; Action = action; Outcome = outcome; Reason = reason; ManifestDigest = manifestDigest;
        ReturnedFields = OwnedValues.Copy(returnedFields); RedactedFields = OwnedValues.Copy(redactedFields);
    }
    public Guid EventId { get; }
    public Guid StreamId { get; }
    public Guid WriterBindingReference { get; }
    public long Sequence { get; }
    public string PreviousEventDigest { get; }
    public DateTimeOffset EventAtUtc { get; }
    public Guid? OperationId { get; }
    public Guid InvocationId { get; }
    public Guid CorrelationId { get; }
    public PublicationAuditActorKindV1 ActorKind { get; }
    public PublicationActorV1? Actor { get; }
    public PublicationScopeV1? Scope { get; }
    public PublicationResourceKindV1? ResourceKind { get; }
    public Guid? ResourceId { get; }
    public PublicationAuditActionV1 Action { get; }
    public PublicationAuditOutcomeV1 Outcome { get; }
    public PublicationAuditReasonV1 Reason { get; }
    public string? ManifestDigest { get; }
    public ImmutableArray<string> ReturnedFields { get; }
    public ImmutableArray<string> RedactedFields { get; }
}
