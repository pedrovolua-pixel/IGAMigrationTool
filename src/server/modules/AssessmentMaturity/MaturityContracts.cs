namespace AssessmentMaturity;

public enum MaturityLevel { Initial, Developing, Defined, Managed, Optimized }
public enum MaturityIndicatorKind { Design, Implementation, MeasuredOperation, RegularReview, ValidatedImprovement }
public enum MaturityIndicatorState { Met, PartiallyMet, NotMet, InsufficientEvidence }
public enum MaturityIssue { InvalidInput, UnknownVersion, InvalidCatalog, DuplicateDomain, UnknownDomain, DuplicateIndicator, InvalidEvidence, InvalidOwnership }

public sealed record MaturityVersions(string AlgorithmVersion, string InputSchemaVersion,
    string BaselineId, string SourceAnalysisFixtureDigest);
/// <summary>Every declared domain is mandatory and remains in the denominator, including missing evidence.</summary>
public sealed record MaturityDomainDefinition(string Id, string Name);
/// <summary>Fixed fictional definitions and thresholds. No catalog-promotion or approval authority.</summary>
public sealed record MaturityCatalog(string Version, int DevelopingPercent, int DefinedPercent,
    int ManagedPercent, int OptimizedPercent, int RequiredDistinctAssessmentCount, string AuthorityBoundary,
    IReadOnlyCollection<MaturityDomainDefinition> MandatoryDomains);
/// <summary>Opaque synthetic references and validation premise, not a production evidence validator.</summary>
public sealed record MaturityIndicatorEvidence(string DomainId, MaturityIndicatorKind Kind,
    MaturityIndicatorState State, IReadOnlyCollection<string> EvidenceReferences,
    IReadOnlyCollection<string> AssessmentReferences, bool HasValidatedImprovementEvidence = false,
    string? ReasonCode = null);
public sealed record MaturityOwnershipEvidence(MaturityIndicatorState State, string? OwnerId,
    IReadOnlyCollection<string> EvidenceReferences, string? ReasonCode = null);
public sealed record MaturityInput(MaturityVersions Versions, MaturityCatalog Catalog,
    IReadOnlyCollection<MaturityIndicatorEvidence> Indicators, MaturityOwnershipEvidence GovernanceOwnership);
public sealed record MaturityDomainAchievement(string DomainId, bool BaseMet,
    bool OperationAndReviewMet, bool ImprovementMet, int InsufficientIndicators);
public sealed record MaturityThresholdMeasure(int MetDomains, int MandatoryDomains, int RequiredPercent, bool IsMet);
public sealed record MaturityGates(MaturityThresholdMeasure Developing, MaturityThresholdMeasure Defined,
    MaturityThresholdMeasure Managed, MaturityThresholdMeasure Optimized, bool GovernanceOwnershipEvidenced);
public sealed record MaturityCounts(int MandatoryDomains, int Indicators, int MetIndicators,
    int PartiallyMetIndicators, int NotMetIndicators, int InsufficientIndicators,
    int InsufficientDomains, int ImprovementMissingDistinctAssessments);
public sealed record MaturityProjection(MaturityInput Input, MaturityLevel Level,
    IReadOnlyList<MaturityDomainAchievement> Domains, MaturityGates Gates, MaturityCounts Counts,
    string InputDigest, string ContentDigest);
public sealed record MaturityResult(MaturityIssue? Issue, MaturityProjection? Projection)
{
    public bool HasProjection => Issue is null && Projection is not null;
}
public sealed record SyntheticMaturityFrozenFixture(MaturityInput Input, string ContentDigest);
