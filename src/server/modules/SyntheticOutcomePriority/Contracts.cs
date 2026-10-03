using System.Collections.Immutable;
using AssessmentCoverage;
using AssessmentScoring;
using Npgsql;

namespace SyntheticOutcomePriority;

public sealed record OutcomeScope(string CustomerId, string ProjectId, string EnvironmentId)
{
    public static OutcomeScope Fixed { get; } = new("synthetic-customer", "synthetic-project", "synthetic-environment");
}
public enum OutcomeRole { Consultant, CustomerOutcomeApprover, QualifiedReviewer, Auditor, Executive, CustomerRiskOwner, PlatformSupport, Mcp }
public enum OutcomeAction { ReadOutcome, ManageOutcome, ApproveOutcome, ReadPlanning, ManagePlanning }
public enum OutcomeResourceState { Mutable, Deleted, Expired, Blocked, Published }
public enum OutcomeOrigin { Documented, Inferred }
public enum OutcomeState { Draft, ConsultantReviewed, CustomerApproved, Superseded, Retired }
public enum OutcomeKind { CreateDraft, Review, Approve, Retire }
public enum PlanningKind { ApproveOriginalEffort, ReplaceEffort, WithdrawEffortOverride, OverridePriority, WithdrawPriorityOverride }
public enum PriorityBand { Low, Medium, High, Immediate }
public enum Exposure { Isolated, Internal, PartnerFacing, PublicFacing }
public enum DependencyCriticality { None, Ordinary, Important, Essential }
public enum EffortSize { XS, S, M, L, XL }
public enum EffortApprovalState { Proposed, ApprovedOriginal, ApprovedReplacement }
public enum OutcomePriorityIssue { InvalidInput, Denied, WrongScope, NotFound, InvalidState, RevisionConflict, SourceConflict, EventConflict, IntegrityMismatch, MigrationDrift, NotInitialized, SourceUnavailable, RevisionOverflow }
public sealed record OutcomeAuthority(string ActorId, bool Authenticated, bool Active, bool Revoked, bool AssignmentActive,
    OutcomeScope AssignedScope, ImmutableArray<OutcomeRole> Roles, ImmutableArray<string> Categories,
    ImmutableArray<OutcomeAction> Actions, OutcomeResourceState ResourceState);
public sealed record OutcomeContentVersion(string OutcomeId, long Version, string CategoryId, string Title, string Behavior,
    OutcomeOrigin Origin, ImmutableArray<CoverageKey> UnitLinks, ImmutableArray<string> ReferenceIds,
    ImmutableArray<string> Assumptions, long? PredecessorVersion, string ContentDigest);
public sealed record OutcomeEvent(Guid EventId, string OutcomeId, long Version, long Revision, OutcomeKind Kind,
    OutcomeState State, string ActorId, OutcomeRole ActorRole, DateTimeOffset RecordedAtUtc, string Reason,
    string ContentDigest, Guid? ReviewedEventId, long? SuccessorVersion);
public sealed record OutcomeEntry(OutcomeContentVersion Content, OutcomeState State, long Revision,
    ImmutableArray<OutcomeEvent> History);
public sealed record OutcomeRegistrySnapshot(OutcomeScope Scope, long Revision, ImmutableArray<OutcomeEntry> Entries,
    ImmutableArray<OutcomeHighWater> HighWater, string ContentDigest);
public sealed record OutcomeHighWater(string OutcomeId, long HighestApprovedVersion);
public sealed record OutcomeCommand(Guid EventId, OutcomeKind Kind, string OutcomeId, long Version,
    long ExpectedRevision, string ExpectedContentDigest, long ExpectedRegistryRevision,
    Guid? ExpectedReviewEventId, OutcomeContentVersion? Content, string Reason);
public sealed record OutcomeSelection(string OutcomeId, long Version, string ContentDigest, long Revision, Guid ApprovalEventId);
public sealed record LockedOutcome(OutcomeContentVersion Content, OutcomeEvent Approval, ImmutableArray<OutcomeEvent> ApprovalHistory);
public sealed record LockedOutcomeSet(string SchemaVersion, OutcomeScope Scope, Guid RunId, string ContractDigest,
    ImmutableArray<LockedOutcome> Outcomes, string ContentDigest);
public sealed record OutcomeReceipt(string SchemaVersion, Guid EventId, string ActorId, string OutcomeId,
    long Version, long Revision, DateTimeOffset RecordedAtUtc, string ContentDigest);
public sealed record CustomerObjective(string ObjectiveId, decimal Weight);
public sealed record PriorityFactorVector(ScoringSeverity Severity, Exposure? Exposure, ImmutableArray<string> AffectedObjectIds,
    DependencyCriticality? Dependency, EffortSize? OriginalEffort, ImmutableArray<CustomerObjective> Objectives,
    ImmutableArray<string> MatchedObjectiveIds);
public sealed record PriorityProjection(string PolicyVersion, decimal? RawPriority, decimal? DisplayPriority,
    PriorityBand? OriginalBand, ImmutableArray<string> MissingInputs, ImmutableArray<PriorityContribution> Contributions);
public sealed record PriorityContribution(string Factor, decimal Normalized, decimal Weight, decimal Points);
public sealed record EffortEstimate(string PolicyVersion, EffortSize Size, int MinimumPersonHours, int MaximumPersonHours);
public sealed record PlanningSourceOption(string FindingId, string OptionId, string CategoryId, PriorityFactorVector Factors);
public sealed record Phase1BPlanningSource(OutcomeScope Scope, Guid RunId, string ProfileId, string ApplicationVersion,
    string ContractDigest, string RunInputDigest, string GuidanceDigest, long SourceRevision,
    ImmutableArray<PlanningSourceOption> Options, string SourceDigest);
public sealed record PlanningEvent(Guid EventId, long Revision, PlanningKind Kind, string ActorId,
    DateTimeOffset RecordedAtUtc, string Reason, string SourceDigest, PriorityBand? PriorityOverride,
    EffortSize? ReplacementSize, ImmutableArray<string> Assumptions);
public sealed record PlanningCommand(Guid EventId, PlanningKind Kind, string OptionId, long ExpectedRevision,
    string ExpectedSourceDigest, PriorityBand? PriorityOverride, EffortSize? ReplacementSize,
    ImmutableArray<string> Assumptions, string Reason);
public sealed record PlanningEntry(PlanningSourceOption Original, string SourceDigest, long Revision,
    PriorityProjection OriginalPriority, PriorityBand? EffectivePriority, EffortEstimate? OriginalEffort,
    EffortEstimate? EffectiveEffort, EffortApprovalState EffortApproval, bool HasPriorityOverride,
    bool HasEffortOverride, ImmutableArray<PlanningEvent> History);
public sealed record PlanningSnapshot(Phase1BPlanningSource Source, ImmutableArray<PlanningEntry> Entries);
public sealed record PlanningReceipt(string SchemaVersion, Guid EventId, Guid RunId, string OptionId, long Revision,
    string ActorId, DateTimeOffset RecordedAtUtc, string SourceDigest);
public sealed record OutcomeRegistryResult(OutcomePriorityIssue? Issue, OutcomeRegistrySnapshot? Snapshot);
public sealed record OutcomeApplyResult(OutcomePriorityIssue? Issue, OutcomeReceipt? Receipt, bool AlreadyApplied = false);
public sealed record OutcomeLockResult(OutcomePriorityIssue? Issue, LockedOutcomeSet? Lock);
public sealed record PlanningReadResult(OutcomePriorityIssue? Issue, PlanningSnapshot? Snapshot);
public sealed record PlanningApplyResult(OutcomePriorityIssue? Issue, PlanningReceipt? Receipt, bool AlreadyApplied = false);
public sealed record PlanningSourceResult(OutcomePriorityIssue? Issue, Phase1BPlanningSource? Source);
public delegate Task<PlanningSourceResult> OutcomePrioritySourceReader(NpgsqlConnection connection, NpgsqlTransaction transaction,
    Guid runId, OutcomeAuthority authority, CancellationToken cancellationToken);
public interface IOutcomePriorityWriteObserver
{
    Task BeforeWriteAsync(string operation, Guid eventId, CancellationToken cancellationToken);
}
