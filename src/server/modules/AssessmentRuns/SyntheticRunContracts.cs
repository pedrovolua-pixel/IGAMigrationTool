using AssessmentCoverage;
using AssessmentOrchestration;

namespace AssessmentRuns;

public enum SyntheticRunState { Planned, Running, Scoring, Failed, Cancelled }
public enum SyntheticAttemptOutcome { Failed, Succeeded, Exhausted }
public enum SyntheticRunIssue
{
    InvalidInput, WrongScope, InvalidPlan, IdempotencyConflict, NotFound,
    RevisionConflict, LeaseUnavailable, StaleLease, InvalidState, CancellationRequested,
    InvalidCoverage, ResultConflict, UnknownWork, MigrationDrift, UnknownEventVersion, InputIntegrityMismatch
}

/// <summary>Explicit fixture settings; these are not production lease/retry defaults.</summary>
public sealed record SyntheticRunPolicy(TimeSpan LeaseDuration, int MaxAttempts, int MaxCheckpointItems = 512);

/// <summary>Value-free synthetic references; no profile, artifact or provider approval is asserted.</summary>
public sealed record SyntheticRunInputVersions(
    string ProfileVersion, string? DesiredOutcomeVersion, string ScoringAlgorithmVersion,
    string AiPolicyVersion, string PromptVersion, string ModelVersion,
    string ApplicationVersion, string WorkSchemaVersion, string ScriptedResultsDigest,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    string? AnalysisFixtureDigest = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    string? MaturityFixtureDigest = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    string? AiPreviewFixtureDigest = null);

public sealed record SyntheticStartRequest(
    SyntheticAuthorizedScope Scope, string IdempotencyKey, string BaselineCatalogId, string ProfileCatalogId,
    CapabilitySnapshot Capability, SyntheticBaselineInventory Baseline, SyntheticRunInputVersions Versions);

public sealed record SyntheticRunLease(string WorkerId, Guid Generation, DateTimeOffset ExpiresAt);
public sealed record SyntheticRunAttempt(
    CoverageKey Key, int AttemptNumber, SyntheticAttemptOutcome Outcome,
    string? ReasonCode, Guid LeaseGeneration, DateTimeOffset CreatedAt);
public sealed record SyntheticCoverageStageSummary(
    CoverageCompletionKind Kind, IReadOnlyList<CoverageStateCount> Counts,
    ExecutableCoverageMeasure ExecutableCoverage, IReadOnlyList<CoverageLimitation> Limitations);

public sealed record SyntheticRunSnapshot(
    Guid RunId, SyntheticAuthorizedScope Scope, string BaselineCatalogId, string ProfileCatalogId,
    SyntheticRunState State, long Revision, bool CancelRequested, long CheckpointSequence,
    string InputDigest, SyntheticRunInputVersions FrozenInputs, SyntheticInventoryPlan Plan,
    IReadOnlyList<CoverageItem> Results, IReadOnlyList<SyntheticRunAttempt> Attempts,
    SyntheticRunLease? Lease, SyntheticCoverageStageSummary? CoverageSummary,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, IReadOnlyList<CoverageKey> InFlightKeys,
    DateTimeOffset ObservedAtDatabaseUtc)
{
    public CoverageProgress Progress => CoverageProgressProjector.Project(Plan.ExpectedKeys, Results).Progress
        ?? throw new InvalidOperationException("Stored synthetic coverage does not reconcile.");
    public bool ScoringPaused => State == SyntheticRunState.Scoring;
}

public sealed record SyntheticRunCommandResult(
    SyntheticRunIssue? Issue, SyntheticRunSnapshot? Snapshot, bool AlreadyApplied = false)
{
    public bool Succeeded => Issue is null && Snapshot is not null;
}

/// <summary>Opaque local work notification; contains no evidence payload or connection information.</summary>
public sealed record SyntheticRunOutboxEvent(
    Guid EventId, Guid RunId, long RunRevision, string SchemaVersion, string MinimumWorkerVersion,
    string Kind, DateTimeOffset CreatedAt, DateTimeOffset? DispatchedAt);
public sealed record SyntheticOutboxDeliveryResult(SyntheticRunIssue? Issue, bool AlreadyApplied);

/// <summary>Only an explicitly injected synthetic test observer; not a production callback.</summary>
public interface ISyntheticRunCommitObserver
{
    Task BeforeCommitAsync(string operation, Guid runId, CancellationToken cancellationToken);
}
