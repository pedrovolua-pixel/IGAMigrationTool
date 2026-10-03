using System.Collections.Immutable;
using AssessmentCoverage;

namespace SyntheticAiExecution;

public sealed record AiScope(string CustomerId, string ProjectId, string EnvironmentId)
{
    public static AiScope Fixed { get; } = new("synthetic-customer", "synthetic-project", "synthetic-environment");
}
public enum AiRole { Consultant, Worker, Auditor, Reviewer, Executive, Support }
public enum AiAction { Read, Dispatch, Reconcile, Override }
public enum AiResourceState { Mutable, Published, Deleted, Cancelled }
public enum AiSeverity { Critical, High, Medium, Low, Informational }
public enum AiScenario { Benign, Empty, RetryTwice, InvalidOutput, InvalidCitation, LostResponse, PermanentUnknown, RetryThenLostResponse }
public enum AiWorkState { Pending, Reserved, Dispatched, Unknown, Retryable, Succeeded, Failed, Cancelled }
public enum AiProviderOutcome { Response, RetryableFailure, Unknown }
public enum AiIssue { InvalidInput, Denied, WrongScope, NotInitialized, MigrationDrift, IntegrityMismatch, SeedConflict, NotFound, RevisionConflict, EventConflict, InvalidState, BudgetExhausted, RetryExhausted, InvalidReceipt, OutputRejected, SourceConflict, Overflow }
public sealed record AiAuthority(string ActorId, AiScope Scope, ImmutableArray<AiRole> Roles, ImmutableArray<AiAction> Actions,
    ImmutableArray<string> Categories, bool Authenticated = true, bool Active = true, bool AssignmentActive = true,
    bool Revoked = false, bool AiPolicyAllowed = true, AiResourceState ResourceState = AiResourceState.Mutable);
public sealed record AiRunLock(Guid RunId, AiScope Scope, string ProfileId, string ApplicationVersion,
    string InputDigest, string BaselineDigest, string ProfileDigest, string SourceDigest, string PolicyVersion,
    string ProviderVersion, string PromptVersion, string FixtureVersion, string MappingDigest, string Epoch, string InitiatingConsultantId);
public sealed record AiUnitMapping(string ProposalId, CoverageKey Key, string ObjectType, string ModuleId, string RuleId,
    string RuleVersion, string Title, AiSeverity Severity, string Impact, string Likelihood, string RootCause,
    ImmutableArray<string> EvidenceIds);
public sealed record AiWork(string WorkId, string Category, string PacketInputJson, ImmutableArray<AiUnitMapping> Units, AiScenario Scenario = AiScenario.Benign);
public sealed record AiAttemptKey(string LogicalKey, Guid AttemptId, int Ordinal, string PacketDigest);
public sealed record AiProviderReceipt(string ReceiptId, AiAttemptKey Attempt, AiProviderOutcome Outcome, int? InputUse, int? OutputUse, string? OutputJson, string? OutputDigest);
public sealed record AiAttemptSnapshot(AiAttemptKey Key, AiWorkState State, bool Held, AiProviderReceipt? Receipt, string PacketInputJson,
    long WorkerGeneration, long WorkRevision);
public sealed record AiFindingOriginal(string OccurrenceId, CoverageKey Key, string ObjectType, string ModuleId, string Category,
    string RuleId, string RuleVersion, string Title, AiSeverity Severity, string Impact, string Likelihood, string RootCause,
    string DetectionMethod, string State, decimal ConfidencePercent, decimal Weight, string ProposalId, string ProposalCanonicalJson,
    ImmutableArray<string> EvidenceIds, string PacketDigest, string ProposalDigest, string OriginalDigest, AiAttemptKey Attempt);
public sealed record AiUnitOutcome(CoverageKey Key, CoverageState State, string? ReasonCode, string Stage, AiFindingOriginal? Finding);
public sealed record AiWorkSnapshot(AiWork Work, AiWorkState State, long Revision, long Generation,
    ImmutableArray<AiAttemptSnapshot> Attempts, ImmutableArray<AiUnitOutcome> Outcomes);
public sealed record AiCounter(string Key, int Charged, int Held, int Allowance, int HardCeiling);
public sealed record AiOverrideCommand(Guid EventId, long ExpectedRevision, int RunTarget, int CategoryTarget, string Category, string Reason);
public sealed record AiOverrideEvent(Guid EventId, long Revision, string ActorId, DateTimeOffset RecordedAtUtc, int RunTarget, int CategoryTarget, string Category, string Reason);
public sealed record AiBudgetSnapshot(long Revision, int RunAllowance, ImmutableArray<KeyValuePair<string, int>> CategoryAllowances,
    ImmutableArray<AiCounter> Counters, ImmutableArray<AiOverrideEvent> History);
public sealed record AiExecutionSnapshot(AiRunLock RunLock, ImmutableArray<AiWorkSnapshot> Works, AiBudgetSnapshot Budget, string ContentDigest);
public sealed record AiCompletion(ImmutableArray<AiUnitOutcome> Outcomes, bool BillingOnly, string ReceiptId);
public sealed record AiOperationResult<T>(AiIssue? Issue, T? Value, bool Replayed = false)
{
    public bool Succeeded => Issue is null;
}
public interface IAiExecutionCommitObserver
{
    Task BeforeWriteAsync(string operation, Guid runId, CancellationToken cancellationToken);
}
public sealed record AiRecovery(AiAttemptKey Attempt, AiScenario Scenario, string? PacketInputJson, ImmutableArray<AiUnitMapping> Units, bool BillingOnly);
public sealed record AiExportAuthority(string ActorId, AiScope Scope, ImmutableArray<AiRole> Roles, ImmutableArray<string> Categories,
    bool Authenticated, bool Active, bool AssignmentActive, bool Revoked, bool ExportGranted, bool CustomerExportAllowed, bool AuditorScopedGrant, AiResourceState ResourceState);
public sealed record AiExportUnitBinding(CoverageKey Key, CoverageState State, string? ReasonCode, string? OccurrenceId, string? OriginalDigest, string? PacketDigest, string? ProposalDigest, AiAttemptKey? Attempt);
public sealed record AiExportVerification(AiRunLock RunLock, ImmutableArray<AiExportUnitBinding> Units, string SourceDigest);
