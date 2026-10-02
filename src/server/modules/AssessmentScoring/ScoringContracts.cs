using AssessmentCoverage;

namespace AssessmentScoring;

public enum ScoringSeverity { Critical, High, Medium, Low, Informational }
public enum ScoringDetectionMethod { Deterministic, AI }
public enum ScoringFindingState
{
    Proposed, AutoConfirmed, Confirmed, Rejected, Deferred, AcceptedRisk,
    RemediationPlanned, InProgress, RemediatedPendingValidation, ValidatedClosed, Reopened
}
public enum ScoringWeightMode { EqualAssessedCategories, ExplicitCategoryWeights }
public enum HealthStatus { Red, Yellow, Green }
public enum ScoringIssue
{
    InvalidInput, UnknownVersion, VersionMismatch, InvalidProfile, InvalidUnit,
    DuplicateUnit, InvalidCoverage, CoverageStateMismatch, UnknownCategory,
    UnknownOutcome, InvalidReviewGate, InvalidValidatedClosure, NumericOverflow
}

/// <summary>Trusted frozen synthetic references, not promotion or baseline-integrity verification.</summary>
public sealed record ScoringVersions(
    string AlgorithmVersion, string InputSchemaVersion, string BaselineId,
    string RuleCatalogVersion, string ProfileVersion, string FrozenInputDigest, string AnalysisContentDigest);
public sealed record ScoringCategoryWeight(string CategoryId, decimal Weight);
public sealed record ScoringProfile(string ProfileVersion, ScoringWeightMode WeightMode,
    IReadOnlyCollection<ScoringCategoryWeight> Categories);
/// <summary>The approval flag is synthetic fixture metadata; it establishes no customer authority.</summary>
public sealed record ScoringOutcome(string Id, bool IsCustomerApprovedFixtureFlag);

/// <summary>
/// One explicit rule/subject unit per unique coverage key in this bounded slice.
/// Grouped presentation and many outcome links do not create additional units.
/// PassingValidationEvidenceId is an opaque synthetic reference, not a validator.
/// </summary>
public sealed record ScoringUnit(
    CoverageKey Key, string CategoryId, string ObjectType, string ModuleId,
    IReadOnlyCollection<string> OutcomeIds, CoverageState CoverageState, decimal CatalogWeight,
    ScoringSeverity? Severity, ScoringDetectionMethod Method, ScoringFindingState? FindingState,
    decimal ConfidencePercent, bool HasPassingValidationEvidence = false, string? PassingValidationEvidenceId = null);

public sealed record ScoringInput(
    ScoringVersions Versions, ScoringProfile Profile,
    IReadOnlyCollection<CoverageKey> ExpectedKeys, IReadOnlyCollection<CoverageItem> Coverage,
    IReadOnlyCollection<ScoringUnit> Units, IReadOnlyCollection<ScoringOutcome> Outcomes);

public sealed record HealthMeasure(decimal? RawScore, decimal? DisplayScore, HealthStatus? Status, int ScoredUnits)
{
    public bool IsAvailable => RawScore is not null;
}
public sealed record GroupHealthMeasure(string Id, HealthMeasure Score);
public sealed record HealthScoreSet(
    HealthMeasure Overall, IReadOnlyList<GroupHealthMeasure> Categories,
    IReadOnlyList<GroupHealthMeasure> ObjectTypes, IReadOnlyList<GroupHealthMeasure> Modules,
    IReadOnlyList<GroupHealthMeasure> ApprovedOutcomes);
public sealed record ScoringQuality(
    int ExpectedKeys, int RepresentedKeys, ExecutableCoverageMeasure ExecutableCoverage,
    IReadOnlyList<CoverageStateCount> CoverageCounts, IReadOnlyList<CoverageLimitation> Limitations,
    int ProposedFindings, int MandatoryReviewFindings, int ReviewedMandatoryFindings,
    int UnreviewedMandatoryFindings, int DeterministicUnits, int AiUnits, int RuleExecutionFailures);
public sealed record ScoringProjection(
    ScoringVersions Versions, ScoringProfile Profile,
    IReadOnlyList<CoverageKey> ExpectedKeys, IReadOnlyList<CoverageItem> Coverage,
    IReadOnlyList<ScoringUnit> Units, IReadOnlyList<ScoringOutcome> Outcomes,
    HealthScoreSet Provisional, HealthScoreSet PublishableCurrent, ScoringQuality Quality, string ContentDigest);
public sealed record ScoringResult(ScoringIssue? Issue, ScoringProjection? Projection)
{
    public bool HasProjection => Issue is null && Projection is not null;
}
