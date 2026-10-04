using System.Runtime.CompilerServices;
using SyntheticEvaluation;
[assembly: InternalsVisibleTo("SyntheticEvaluationWorkflow.Tests")]
[assembly: InternalsVisibleTo("SyntheticEvaluationWorkflowStore.Tests")]
[assembly: InternalsVisibleTo("SyntheticEvaluationWorkflowIndependent.Tests")]
namespace SyntheticEvaluationWorkflow;

public enum EvaluationWorkflowCommandKind { Review, PresentationCorrection }
public enum EvaluationWorkflowIssue
{
    InvalidInput, Denied, NotFound, RevisionConflict, RegistryConflict, SourceConflict, EventConflict,
    IntegrityMismatch, MigrationDrift, NotInitialized, SeedConflict, RevisionOverflow, ClockConflict, WorkflowLimit
}
public sealed record EvaluationWorkflowCorrection(string? Severity, string? Category, string? RootCause, string? Recommendation);
public sealed record EvaluationWorkflowCommand(Guid EventId, string MemberId, EvaluationWorkflowCommandKind Kind,
    long ExpectedAggregateRevision, long ExpectedMemberRevision, string ExpectedRegistryVersionId,
    string ExpectedSourceDigest, string ExpectedSampleDigest, EvaluationReviewOutcome? Outcome,
    EvaluationOriginClassification? OriginatingClassification, string Reason, IReadOnlyList<string> EvidenceReferenceIds,
    EvaluationWorkflowCorrection? Correction);
public sealed record EvaluationWorkflowOriginal(string MemberId, string Title, string Severity, string Category,
    string RootCause, string Recommendation, IReadOnlyList<string> EvidenceReferenceIds);
public sealed record EvaluationWorkflowSeed(SamplingInput Sampling, EvaluationReviewerRegistryInput Registry,
    IReadOnlyList<EvaluationWorkflowOriginal> Originals);
public sealed record EvaluationWorkflowActor(string IdentityId);
public sealed record EvaluationWorkflowEvent(Guid EventId, long Sequence, long AggregateRevision, long MemberRevision,
    string MemberId, EvaluationWorkflowCommandKind Kind, string ActorId, string AssignmentId, string RegistryVersionId,
    string RegistryDigest, DateTimeOffset RecordedAtUtc, EvaluationReviewOutcome RecordedOutcome,
    EvaluationOriginClassification? OriginatingClassification, string Reason, IReadOnlyList<string> EvidenceReferenceIds,
    EvaluationWorkflowCorrection? Correction, string CommandDigest, string PreviousEventDigest, string ContentDigest);
public sealed record EvaluationWorkflowReceipt(Guid EventId, string MemberId, string ActorId, long AggregateRevision,
    long MemberRevision, long Version, string SnapshotDigest, DateTimeOffset RecordedAtUtc);
public sealed record EvaluationWorkflowMember(EvaluationWorkflowOriginal Original, EvaluationMemberMetadata OriginMetadata,
    string AssignmentId, long Revision, EvaluationReviewOutcome Outcome, EvaluationOriginClassification? OriginatingClassification,
    bool CanReview, bool CanCorrectPresentation, bool AuthorizedContextSufficient, EvaluationWorkflowCorrection? CurrentCorrection);
public sealed record EvaluationWorkflowVersion(long Version, DateTimeOffset CorrectionCutoffUtc, long LastEventSequence,
    string RegistryVersionId, string RegistryDigest, string VersionManifestJson, string VersionManifestDigest,
    string? PredecessorDigest, string AccuracyCanonicalJson, string AccuracyDigest, string WarningCanonicalJson,
    string WarningDigest, string SnapshotCanonicalJson, string ContentDigest);
public sealed record EvaluationWorkflowSnapshot(string SchemaVersion, string ActorId, long AggregateRevision,
    string RegistryVersionId, string SourceDigest, string PopulationDigest, string SampleDigest,
    DateTimeOffset SampleCorrectionCutoffUtc, IReadOnlyList<EvaluationWorkflowMember> Members,
    IReadOnlyList<EvaluationWorkflowVersionSummary> Versions);
public sealed record EvaluationWorkflowVersionSummary(long Version, DateTimeOffset CorrectionCutoffUtc,
    long LastEventSequence, string RegistryVersionId, string ContentDigest, string? PredecessorDigest);
public sealed record EvaluationWorkflowHistory(string MemberId, long AggregateRevision, long MemberRevision,
    string RegistryVersionId, IReadOnlyList<EvaluationWorkflowEvent> Events, long? NextAfterSequence);
public sealed record EvaluationWorkflowReadResult(EvaluationWorkflowIssue? Issue, EvaluationWorkflowSnapshot? Snapshot);
public sealed record EvaluationWorkflowApplyResult(EvaluationWorkflowIssue? Issue, EvaluationWorkflowReceipt? Receipt, bool AlreadyApplied = false);
public sealed record EvaluationWorkflowSeedResult(EvaluationWorkflowIssue? Issue, bool AlreadySeeded = false);
public sealed record EvaluationWorkflowHistoryReadResult(EvaluationWorkflowIssue? Issue, EvaluationWorkflowHistory? History);
public sealed record EvaluationWorkflowMemberReadResult(EvaluationWorkflowIssue? Issue, EvaluationWorkflowMember? Member,
    long? AggregateRevision, string? RegistryVersionId, string? SourceDigest, string? SampleDigest);
public sealed record EvaluationWorkflowVersionReadResult(EvaluationWorkflowIssue? Issue, EvaluationWorkflowVersion? Version);
public sealed record EvaluationWorkflowRegistryResult(EvaluationWorkflowIssue? Issue, string? RegistryVersionId = null,
    long? AggregateRevision = null, long? Version = null, string? SnapshotDigest = null);
public interface ISyntheticEvaluationWorkflowCommitObserver
{
    Task BeforeCommitAsync(string operation, Guid? eventId, CancellationToken cancellationToken);
}
internal sealed record WorkflowSource(string SchemaVersion, string ScopeId, SamplingInput Sampling, IReadOnlyList<EvaluationWorkflowOriginal> Originals);
internal sealed record WorkflowCurrent(string MemberId, long Revision, EvaluationReviewOutcome Outcome,
    EvaluationOriginClassification? OriginatingClassification, EvaluationWorkflowCorrection? Correction);
internal sealed record WorkflowRegistry(long Revision, EvaluationReviewerRegistryInput Input, string Json, string Digest, DateTimeOffset AtUtc);
internal sealed record WorkflowStoredEvent(EvaluationWorkflowCommand Command, EvaluationWorkflowEvent Event, EvaluationWorkflowReceipt Receipt);
internal sealed class WorkflowInvalidException(EvaluationWorkflowIssue issue) : Exception
{
    internal EvaluationWorkflowIssue Issue { get; } = issue;
}
