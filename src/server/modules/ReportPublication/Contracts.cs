using System.Collections.Immutable;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("ReportPublication.Tests")]

namespace ReportPublication;

public enum PublicationIssueV1 { InvalidInput, Unavailable, RevisionConflict, IdempotencyConflict, IntegrityMismatch, DependencyUnavailable }
public enum AssessmentStateV1 { Completed, CompletedWithGaps }
public enum PublicationApprovalV1 { Published, PublishedWithWarnings }
public enum PublicationAvailabilityV1 { Available, Unavailable, Redacted }
public enum PublicationReasonV1 { None, NotApplicable, NotAssessed, InsufficientEvidence, Excluded, Inaccessible, Redacted, Unsupported, Error, Expired, Deleted }
public enum PublicationWarningKindV1 { MandatoryReviewIncomplete, CoverageIncomplete, SourceLimitation }
public enum PublicationBlobKindV1 { Projection, Score, Manifest }
public enum PublicationDimensionKindV1 { Category, ObjectType, Module, DesiredOutcome }
public enum PublicationCoverageStateV1 { Pass, Finding, NotApplicable, NotAssessed, InsufficientEvidence, Excluded, Inaccessible, Redacted, Unsupported, Error }
public enum PublicationSeverityV1 { Critical, High, Medium, Low, Informational }
public enum PublicationFindingStateV1 { Proposed, AutoConfirmed, Confirmed, Rejected, Deferred, AcceptedRisk, RemediationPlanned, InProgress, RemediatedPendingValidation, ValidatedClosed, Reopened }
public enum PublicationFindingMethodV1 { Deterministic, AI }
public enum PublicationMaturityLevelV1 { Initial, Developing, Defined, Managed, Optimized }
public enum PublicationReviewStateV1 { Unverified, Reviewed }
public enum PublicationRiskStateV1 { Current, ReviewRequired }
public enum PublicationAuditActionV1 { Publish, ReadExact }
public enum PublicationAuditOutcomeV1 { Succeeded, Denied, Failed, Cancelled }
public enum PublicationAuditReasonV1 { None, InvalidInput, AuthorityDenied, SourceUnavailable, RevisionConflict, IdempotencyConflict, IntegrityMismatch, LifecycleDenied, DependencyUnavailable, Cancelled }
public enum PublicationAuditActorKindV1 { Human, Anonymous }
public enum PublicationResourceKindV1 { Run, ReportVersion }
public enum PublicationOperationalSignalV1 { AuditUnavailable, CommitOutcomeUnknown }

public sealed record PublicationScopeV1(Guid CustomerId, Guid ProjectId, Guid EnvironmentId, Guid AssessmentId);
public sealed record PublicationActorV1(Guid TenantId, Guid ObjectId, Guid SessionId, long SecurityVersion);
public sealed record PublicationWarningV1(PublicationWarningKindV1 Kind, Guid RecordId, string Category);
public sealed record ExactReportRequestV1(PublicationScopeV1 Scope, Guid ReportVersionId, string ExpectedManifestDigest, Guid InvocationId, Guid CorrelationId);

public sealed class PublishCommandV1
{
    public PublishCommandV1(Guid operationId, Guid invocationId, Guid correlationId, PublicationScopeV1 scope,
        Guid runId, long expectedRunRevision, string expectedSourceDigest, IEnumerable<PublicationWarningV1> acknowledgedWarnings)
    {
        OperationId = operationId; InvocationId = invocationId; CorrelationId = correlationId; Scope = scope;
        RunId = runId; ExpectedRunRevision = expectedRunRevision; ExpectedSourceDigest = expectedSourceDigest;
        AcknowledgedWarnings = OwnedValues.Copy(acknowledgedWarnings);
    }
    public Guid OperationId { get; }
    public Guid InvocationId { get; }
    public Guid CorrelationId { get; }
    public PublicationScopeV1 Scope { get; }
    public Guid RunId { get; }
    public long ExpectedRunRevision { get; }
    public string ExpectedSourceDigest { get; }
    public ImmutableArray<PublicationWarningV1> AcknowledgedWarnings { get; }
}

public sealed record PublicationInputsV1(Guid BaselineId, string BaselineDigest, string CapabilityLockDigest,
    string RuleCatalogVersion, string RuleCatalogDigest, string ScoringProfileVersion, string ScoringProfileDigest,
    string MaturityProfileVersion, string MaturityProfileDigest, string? DesiredOutcomeVersion, string? DesiredOutcomeDigest,
    string ScoringAlgorithmVersion, string MaturityAlgorithmVersion, string ApplicationVersion,
    string ReviewSnapshotDigest, string CoverageSnapshotDigest, string RunInputDigest,
    string AiPolicyVersion, string? ModelVersion, string? PromptVersion);
public sealed record PublicationRetentionV1(Guid PolicyId, string PolicyVersion, DateTimeOffset ClockStartUtc,
    DateTimeOffset ExpiresAtUtc, Guid? HoldReference);
public sealed record PublicationProvenanceV1(string Kind, Guid OpaqueRecordId, string Digest);
public sealed record PublicationRedactionMarkerV1(string Section, Guid Id, string Field, PublicationReasonV1 Reason);
public sealed record PublicationBlobDescriptorV1(PublicationBlobKindV1 Kind, string Digest, long ByteLength);
public sealed record PublicationDisplayRecordV1(Guid Id, string Category, string Title, string? Text,
    PublicationAvailabilityV1 Availability, PublicationReasonV1 Reason);
public sealed record PublicationScoreValueV1(PublicationAvailabilityV1 Availability, decimal? Value, PublicationReasonV1 Reason);
public sealed record PublicationScoreV1(PublicationScoreValueV1 Provisional, PublicationScoreValueV1 Publishable, PublicationScoreValueV1 Quality);
public sealed record PublicationMaturityV1(PublicationAvailabilityV1 Availability, PublicationMaturityLevelV1? Level,
    string AlgorithmVersion, string InputDigest, string ContentDigest, PublicationReasonV1 Reason);
public sealed record PublicationDimensionV1(PublicationDimensionKindV1 Kind, Guid Id, string Category,
    PublicationScoreValueV1 Provisional, PublicationScoreValueV1 Publishable);
public sealed record PublicationCoverageItemV1(Guid Id, string Category, PublicationCoverageStateV1 State, PublicationReasonV1 Reason);
public sealed record PublicationCoverageCountsV1(long Planned, long Executed, long Gap, long NotApplicable);
public sealed record PublicationProtectedReferenceV1(Guid Id, string Category, PublicationAvailabilityV1 Availability, PublicationReasonV1 Reason);
public sealed record PublicationSourceDisplayValueV1(PublicationAvailabilityV1 Availability, string? Value,
    PublicationReasonV1 Reason, Guid? SourceReference);
public sealed record PublicationRecommendationV1(Guid Id, Guid FindingId, string Category, string Summary,
    PublicationSourceDisplayValueV1 Priority, PublicationSourceDisplayValueV1 Effort, PublicationReviewStateV1 ReviewState);
public sealed record PublicationAcceptedRiskV1(Guid Id, Guid FindingId, string Category, Guid DecisionReference,
    DateTimeOffset ReviewAtUtc, PublicationRiskStateV1 Status);
public sealed record PublicationHealthyControlV1(Guid Id, string Category, string RuleId, string RuleVersion, string Summary);

public sealed class PublicationFindingV1
{
    public PublicationFindingV1(Guid id, string category, string title, string summary, PublicationSeverityV1 severity,
        PublicationFindingStateV1 state, PublicationFindingMethodV1 method, decimal? confidencePercent, string? confidenceBand,
        PublicationAvailabilityV1 confidenceAvailability, PublicationReasonV1 confidenceReason, bool mandatoryReview,
        IEnumerable<Guid> referenceIds, IEnumerable<Guid> rootCauseIds, string originalDigest, long reviewRevision)
    {
        Id = id; Category = category; Title = title; Summary = summary; Severity = severity; State = state; Method = method;
        ConfidencePercent = confidencePercent; ConfidenceBand = confidenceBand; ConfidenceAvailability = confidenceAvailability;
        ConfidenceReason = confidenceReason; MandatoryReview = mandatoryReview;
        ReferenceIds = OwnedValues.Copy(referenceIds); RootCauseIds = OwnedValues.Copy(rootCauseIds);
        OriginalDigest = originalDigest; ReviewRevision = reviewRevision;
    }
    public Guid Id { get; }
    public string Category { get; }
    public string Title { get; }
    public string Summary { get; }
    public PublicationSeverityV1 Severity { get; }
    public PublicationFindingStateV1 State { get; }
    public PublicationFindingMethodV1 Method { get; }
    public decimal? ConfidencePercent { get; }
    public string? ConfidenceBand { get; }
    public PublicationAvailabilityV1 ConfidenceAvailability { get; }
    public PublicationReasonV1 ConfidenceReason { get; }
    public bool MandatoryReview { get; }
    public ImmutableArray<Guid> ReferenceIds { get; }
    public ImmutableArray<Guid> RootCauseIds { get; }
    public string OriginalDigest { get; }
    public long ReviewRevision { get; }
}

public sealed class PublicationRootCauseV1
{
    public PublicationRootCauseV1(Guid id, string category, string? summary, IEnumerable<Guid> findingIds,
        PublicationAvailabilityV1 availability, PublicationReasonV1 reason)
    { Id = id; Category = category; Summary = summary; FindingIds = OwnedValues.Copy(findingIds); Availability = availability; Reason = reason; }
    public Guid Id { get; }
    public string Category { get; }
    public string? Summary { get; }
    public ImmutableArray<Guid> FindingIds { get; }
    public PublicationAvailabilityV1 Availability { get; }
    public PublicationReasonV1 Reason { get; }
}

public sealed class PublicationCoverageV1
{
    public PublicationCoverageV1(IEnumerable<PublicationCoverageItemV1> items, PublicationCoverageCountsV1 counts)
    { Items = OwnedValues.Copy(items); Counts = counts; }
    public ImmutableArray<PublicationCoverageItemV1> Items { get; }
    public PublicationCoverageCountsV1 Counts { get; }
}

public sealed class PublicationAppendicesV1
{
    public PublicationAppendicesV1(IEnumerable<PublicationDisplayRecordV1> notes, IEnumerable<PublicationProtectedReferenceV1> protectedReferences)
    { Notes = OwnedValues.Copy(notes); ProtectedReferences = OwnedValues.Copy(protectedReferences); }
    public ImmutableArray<PublicationDisplayRecordV1> Notes { get; }
    public ImmutableArray<PublicationProtectedReferenceV1> ProtectedReferences { get; }
}

/// <summary>Typed minimized report input. No generated original, comments, raw values, code or locator slots.</summary>
public sealed class PublicationProjectionV1
{
    public PublicationProjectionV1(PublicationScopeV1 scope, Guid runId, long runRevision, PublicationInputsV1 inputs,
        IEnumerable<PublicationDisplayRecordV1> executiveSummary, IEnumerable<PublicationDisplayRecordV1> environmentScope,
        PublicationScoreV1 scores, PublicationMaturityV1 maturity, IEnumerable<PublicationDimensionV1> dimensions,
        PublicationCoverageV1 coverage, IEnumerable<PublicationFindingV1> findings, IEnumerable<PublicationRootCauseV1> rootCauses,
        IEnumerable<PublicationHealthyControlV1> healthyControls, IEnumerable<PublicationRecommendationV1> recommendations,
        IEnumerable<PublicationAcceptedRiskV1> acceptedRisks, IEnumerable<PublicationWarningV1> warnings,
        IEnumerable<PublicationDisplayRecordV1> methodology, PublicationAppendicesV1 technicalAppendices,
        IEnumerable<PublicationRedactionMarkerV1> redactionMarkers)
    {
        Scope = scope; RunId = runId; RunRevision = runRevision; Inputs = inputs;
        ExecutiveSummary = OwnedValues.Copy(executiveSummary); EnvironmentScope = OwnedValues.Copy(environmentScope);
        Scores = scores; Maturity = maturity; Dimensions = OwnedValues.Copy(dimensions); Coverage = coverage;
        Findings = OwnedValues.Copy(findings); RootCauses = OwnedValues.Copy(rootCauses); HealthyControls = OwnedValues.Copy(healthyControls);
        Recommendations = OwnedValues.Copy(recommendations); AcceptedRisks = OwnedValues.Copy(acceptedRisks); Warnings = OwnedValues.Copy(warnings);
        Methodology = OwnedValues.Copy(methodology); TechnicalAppendices = technicalAppendices; RedactionMarkers = OwnedValues.Copy(redactionMarkers);
    }
    public PublicationScopeV1 Scope { get; }
    public Guid RunId { get; }
    public long RunRevision { get; }
    public PublicationInputsV1 Inputs { get; }
    public ImmutableArray<PublicationDisplayRecordV1> ExecutiveSummary { get; }
    public ImmutableArray<PublicationDisplayRecordV1> EnvironmentScope { get; }
    public PublicationScoreV1 Scores { get; }
    public PublicationMaturityV1 Maturity { get; }
    public ImmutableArray<PublicationDimensionV1> Dimensions { get; }
    public PublicationCoverageV1 Coverage { get; }
    public ImmutableArray<PublicationFindingV1> Findings { get; }
    public ImmutableArray<PublicationRootCauseV1> RootCauses { get; }
    public ImmutableArray<PublicationHealthyControlV1> HealthyControls { get; }
    public ImmutableArray<PublicationRecommendationV1> Recommendations { get; }
    public ImmutableArray<PublicationAcceptedRiskV1> AcceptedRisks { get; }
    public ImmutableArray<PublicationWarningV1> Warnings { get; }
    public ImmutableArray<PublicationDisplayRecordV1> Methodology { get; }
    public PublicationAppendicesV1 TechnicalAppendices { get; }
    public ImmutableArray<PublicationRedactionMarkerV1> RedactionMarkers { get; }
}

/// <summary>Only an owning in-module adapter or explicitly trusted friend may create this source proof.</summary>
public sealed class SourceCaptureV1
{
    internal SourceCaptureV1(PublicationProjectionV1 projection, AssessmentStateV1 assessmentState,
        PublicationRetentionV1 retention, IEnumerable<string> requiredCategories, IEnumerable<string> requiredFields,
        IEnumerable<PublicationProvenanceV1> provenance)
    {
        Projection = projection; AssessmentState = assessmentState; Retention = retention;
        RequiredCategories = OwnedValues.Copy(requiredCategories); RequiredFields = OwnedValues.Copy(requiredFields); Provenance = OwnedValues.Copy(provenance);
    }
    public PublicationProjectionV1 Projection { get; }
    public AssessmentStateV1 AssessmentState { get; }
    public PublicationRetentionV1 Retention { get; }
    public ImmutableArray<string> RequiredCategories { get; }
    public ImmutableArray<string> RequiredFields { get; }
    public ImmutableArray<PublicationProvenanceV1> Provenance { get; }
}

internal static class OwnedValues
{
    internal static ImmutableArray<T> Copy<T>(IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        // Copy even an ImmutableArray input: no caller backing storage is adopted.
        return ImmutableArray.CreateRange(values.ToArray());
    }
}
