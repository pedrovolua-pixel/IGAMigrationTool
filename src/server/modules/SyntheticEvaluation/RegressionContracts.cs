namespace SyntheticEvaluation;

public enum EvaluationOriginSeverity { Critical, High, Medium, Low, Informational }
public enum EvaluationBreakdownDimension { Environment, Category, Severity, Module, Rule, ModelPrompt, ConfidenceBand, DesiredOutcomeVersion }
public enum EvaluationComparisonIssue
{
    InvalidInput, InvalidReference, InvalidVersion, InvalidMetadata, DuplicateMetadata, MissingMetadata,
    UnexpectedMetadata, InvalidRequiredStratum, DuplicateRequiredStratum, InvalidCoverageAttribution, IncompatibleComparison
}
public enum EvaluationSafetyAssessment { NotAssessed, AssessedNoEvent, Event }
public enum EvaluationCoverageCause { CandidateCaused, ExternalEvidence, Unknown }
public enum EvaluationRegressionStatus { Blocked, NotVerified, NoRegressionDetected }
public enum EvaluationRegressionRule
{
    OverallAccuracy, UnauthorizedCitation, ProtectedDataDisclosure, InstructionFollowing,
    MissingFactInferenceDistinction, CriticalHighDecline, RejectionGrowth, RequiredStratumCoverage
}
public enum EvaluationRuleState { NoBlockDetected, Blocked, NotVerified, NotApplicable }

public sealed record EvaluationMemberMetadata(string MemberId, EvaluationOriginSeverity OriginSeverity,
    string EnvironmentId, string ModuleId, string CategoryId, string RuleId, string ModelPromptId, string ConfidenceBandId);
public sealed record EvaluationWarningInput(EvaluationProjection Accuracy, IReadOnlyList<EvaluationMemberMetadata> Metadata);
public sealed record EvaluationRequiredStratum(string EnvironmentId, string ModuleId, string CategoryId, EvaluationOriginSeverity Severity);
public sealed record EvaluationComparisonLocks(string RepresentativeSetId, string InputPopulationDigest,
    string ReviewerInstructionDigest, string SourceMetadataDigest);
public sealed record EvaluationSafetyDeclarations(EvaluationSafetyAssessment UnauthorizedCitation,
    EvaluationSafetyAssessment ProtectedDataDisclosure, EvaluationSafetyAssessment InstructionFollowing,
    EvaluationSafetyAssessment MissingFactInferenceDistinction);
public sealed record EvaluationStratumAttribution(EvaluationRequiredStratum Stratum, EvaluationCoverageCause Cause);
public sealed record EvaluationRegressionInput(EvaluationWarningProjection Baseline, EvaluationWarningProjection Candidate,
    EvaluationComparisonLocks BaselineLocks, EvaluationComparisonLocks CandidateLocks,
    IReadOnlyList<EvaluationRequiredStratum> RequiredStrata, EvaluationSafetyDeclarations CandidateSafety,
    IReadOnlyList<EvaluationStratumAttribution> CoverageAttributions);
public sealed record EvaluationWarningSummary(EvaluationAccuracy Counts, bool LowSampleWarning);
public sealed record EvaluationBreakdown(EvaluationBreakdownDimension Dimension, string Key, EvaluationWarningSummary Summary);
public sealed record EvaluationRegressionRuleResult(EvaluationRegressionRule Rule, EvaluationRuleState State,
    EvaluationRequiredStratum? Stratum, EvaluationAccuracy? BaselineCounts, EvaluationAccuracy? CandidateCounts,
    EvaluationCoverageCause? CoverageCause);
public sealed record EvaluationWarningResult(EvaluationComparisonIssue? Issue, EvaluationWarningProjection? Projection)
{
    public bool HasProjection => Issue is null && Projection is not null;
}
public sealed record EvaluationRegressionResult(EvaluationComparisonIssue? Issue, EvaluationRegressionProjection? Projection)
{
    public bool HasProjection => Issue is null && Projection is not null;
}

public sealed class EvaluationWarningProjection
{
    internal EvaluationWarningProjection(EvaluationProjection accuracy, EvaluationMemberMetadata[] metadata,
        EvaluationBreakdown[] breakdowns, string canonicalJson, string contentDigest)
    {
        Accuracy = accuracy;
        Metadata = Array.AsReadOnly((EvaluationMemberMetadata[])metadata.Clone());
        GeneralAi = new(accuracy.GeneralAi, accuracy.GeneralAi.Denominator < 30);
        DesiredOutcome = new(accuracy.DesiredOutcome, accuracy.DesiredOutcome.Denominator < 30);
        Breakdowns = Array.AsReadOnly((EvaluationBreakdown[])breakdowns.Clone());
        CanonicalJson = canonicalJson;
        ContentDigest = contentDigest;
    }
    public EvaluationProjection Accuracy { get; }
    public IReadOnlyList<EvaluationMemberMetadata> Metadata { get; }
    public EvaluationWarningSummary GeneralAi { get; }
    public EvaluationWarningSummary DesiredOutcome { get; }
    public IReadOnlyList<EvaluationBreakdown> Breakdowns { get; }
    public string CanonicalJson { get; }
    public string ContentDigest { get; }
}

public sealed class EvaluationRegressionProjection
{
    internal EvaluationRegressionProjection(EvaluationRegressionStatus status, EvaluationRegressionRuleResult[] rules,
        string baselineDigest, string candidateDigest, string canonicalJson, string contentDigest)
    {
        Status = status;
        Rules = Array.AsReadOnly((EvaluationRegressionRuleResult[])rules.Clone());
        BaselineDigest = baselineDigest;
        CandidateDigest = candidateDigest;
        CanonicalJson = canonicalJson;
        ContentDigest = contentDigest;
    }
    public EvaluationRegressionStatus Status { get; }
    public IReadOnlyList<EvaluationRegressionRuleResult> Rules { get; }
    public string BaselineDigest { get; }
    public string CandidateDigest { get; }
    public string CanonicalJson { get; }
    public string ContentDigest { get; }
}
