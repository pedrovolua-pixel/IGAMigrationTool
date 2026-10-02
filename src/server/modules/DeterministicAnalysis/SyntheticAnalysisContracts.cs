using System.Collections.Immutable;
using AssessmentCoverage;

namespace DeterministicAnalysis;

public enum SyntheticFactKind { GuardFailureCount, TraceFailureCount, PendingMarkerCount, DuplicateMarkerCount, UnusedMarkerCount }
public enum SyntheticFactAvailability { Known, Missing, Conflicting, Redacted }
public enum SyntheticSeverity { Critical, High, Medium, Low, Informational }
public enum SyntheticInitialDisposition { Proposed, AutoConfirmed }
public enum SyntheticPredicate { CountGreaterThan }
public enum SyntheticAnalysisIssue { InvalidInput, WrongScope, UnknownPreset, UnknownProfile, FrozenInputMismatch, IncompleteCoverage, ResultMismatch }

public sealed record SyntheticAnalysisScope(string CustomerId, string ProjectId, string EnvironmentId);
public sealed record SyntheticCompatibility(string SourceProduct, string ProductVersion, string EvidenceSchemaVersion, string RuleLanguageVersion);
public sealed record SyntheticFact(SyntheticFactKind Kind, SyntheticFactAvailability Availability, int? Count);
public sealed record SyntheticEvidenceObject(
    string ObjectId, string ObjectType, string ModuleId, string EvidenceReference,
    SyntheticCompatibility Compatibility, bool Excluded, ImmutableArray<SyntheticFact> Facts);
public sealed record SyntheticRecommendationOption(string Id, string Text, string Prerequisites, string Risk, string RecoveryGuidance);
public sealed record SyntheticRule(
    string Id, string Version, string Title, string CategoryId, string ModuleId, string ObjectType,
    SyntheticFactKind RequiredFact, SyntheticPredicate Predicate, int Threshold, SyntheticSeverity Severity,
    decimal Weight, bool AutoConfirm, string Purpose, string Risk, string Applicability,
    string ResultBehavior, string GuidanceReference, string Impact, string Likelihood, string RootCause,
    ImmutableArray<string> Assumptions, ImmutableArray<SyntheticRecommendationOption> Options,
    ImmutableArray<string> ValidationGuidance, ImmutableArray<string> Limitations,
    ImmutableArray<string> KnownFalsePositiveConditions);
public sealed record SyntheticAnalysisPreset(string Id, string Version, string Label, ImmutableArray<SyntheticEvidenceObject> Objects);
public sealed record SyntheticCategoryWeight(string CategoryId, decimal Weight);
public sealed record SyntheticAnalysisProfile(
    string Id, string Version, string Label, string AlgorithmVersion, string GapPolicy,
    ImmutableArray<SyntheticCategoryWeight> CategoryWeights);

/// <summary>Private synthetic lock only; never a customer authority, promoted catalog or public contract.</summary>
public sealed record SyntheticAnalysisLock(
    SyntheticAnalysisScope Scope, string PackVersion, string PackDigest, string PresetId, string PresetVersion,
    string EvidenceDigest, string CatalogVersion, string CatalogDigest, string ProfileId, string ProfileVersion,
    string ProfileDigest, SyntheticCompatibility Compatibility);
public sealed record SyntheticAnalysisUnit(
    string UnitId, CoverageKey Key, string RuleId, string RuleVersion, string ObjectId,
    string CategoryId, string ModuleId, string ObjectType, decimal Weight, ImmutableArray<string> OutcomeIds);
public sealed record SyntheticRuleEvaluation(CoverageState State, string? ReasonCode, string ResponsibleStage, SyntheticFact? ObservedFact);
public sealed record SyntheticAnalysisRuleResult(SyntheticAnalysisUnit Unit, CoverageItem Coverage, SyntheticFact? ObservedFact, string EvidenceReference);
public sealed record SyntheticAnalysisPlan(
    SyntheticAnalysisLock FrozenInputs, ImmutableArray<SyntheticAnalysisUnit> Units,
    ImmutableArray<SyntheticEvidenceObject> Evidence, ImmutableArray<SyntheticAnalysisRuleResult> Results,
    string ResultDigest)
{
    public ImmutableArray<CoverageKey> ExpectedKeys => Units.Select(unit => unit.Key).ToImmutableArray();
    public ImmutableArray<CoverageItem> ExpectedResults => Results.Select(result => result.Coverage).ToImmutableArray();
}

public sealed record SyntheticObservedFact(string EvidenceReference, SyntheticFactKind Kind, int Count, string Description);
public sealed record SyntheticFindingProvenance(
    Guid RunId, string PackVersion, string PackDigest, string CatalogVersion, string CatalogDigest, string RuleId, string RuleVersion,
    string BaselineId, string BaselineVersion, string EvidenceDigest, string EvidenceReference,
    SyntheticAnalysisScope Scope, SyntheticCompatibility Compatibility);
/// <summary>Immutable generated original. There is deliberately no disposition/edit operation in this module.</summary>
public sealed record SyntheticGeneratedFinding(
    string OccurrenceId, string RootCauseKey, string Title, string CategoryId, string ObjectId,
    string ObjectType, string ModuleId, ImmutableArray<string> OutcomeIds, string DetectionMethod,
    SyntheticFindingProvenance Provenance, ImmutableArray<SyntheticObservedFact> Facts, string Inference,
    ImmutableArray<string> Assumptions, SyntheticSeverity Severity, decimal ConfidencePercent, string ConfidenceBand,
    string Impact, string Likelihood, string RootCause, string GuidanceReference,
    ImmutableArray<SyntheticRecommendationOption> RecommendationOptions, ImmutableArray<string> ValidationGuidance,
    ImmutableArray<string> Limitations, SyntheticInitialDisposition InitialDisposition, string ReviewPolicy,
    string GeneratedOriginalDigest);
public sealed record SyntheticRootCauseGroup(
    string RootCauseKey, string RuleId, string RuleVersion, ImmutableArray<string> OccurrenceIds,
    ImmutableArray<string> ObjectIds);
public sealed record SyntheticAnalysisResult(
    Guid RunId, SyntheticAnalysisLock FrozenInputs, ImmutableArray<SyntheticAnalysisRuleResult> Results,
    ImmutableArray<SyntheticGeneratedFinding> Findings, ImmutableArray<SyntheticRootCauseGroup> Groups,
    string SavedCoverageDigest, string ContentDigest);
public sealed record SyntheticAnalysisResponse(SyntheticAnalysisIssue? Issue, SyntheticAnalysisResult? Analysis)
{
    public bool Succeeded => Issue is null && Analysis is not null;
}
