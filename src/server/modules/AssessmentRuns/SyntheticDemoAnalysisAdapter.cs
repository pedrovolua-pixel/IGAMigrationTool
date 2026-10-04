using AssessmentCoverage;
using AssessmentScoring;
using DeterministicAnalysis;

namespace AssessmentRuns;

public sealed record SyntheticDemoAnalysisProjection(SyntheticAnalysisResult Analysis, ScoringProjection Scoring);
public sealed record SyntheticDemoAnalysisResponse(string? ReasonCode, SyntheticDemoAnalysisProjection? Projection)
{
    public bool IsAvailable => ReasonCode is null && Projection is not null;
}

/// <summary>Read-only, reproducible projection from an exact complete saved synthetic run. No lifecycle transition or publication.</summary>
public static class SyntheticDemoAnalysisAdapter
{
    public static SyntheticDemoAnalysisResponse Project(SyntheticRunSnapshot? run,
        IReadOnlyDictionary<string, ScoringFindingState>? reviewedStates = null, string? reviewSnapshotDigest = null)
    {
        if (run is null || run.RunId == Guid.Empty || run.Scope != DemoFixtureCatalog.Scope) return Deny("invalid_synthetic_scope");
        if (run.InputDigest is not { Length: 64 } || !run.InputDigest.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f')) return Deny("invalid_input_digest");
        if (!DemoAnalysisCatalog.IsAnalysisBaseline(run.BaselineCatalogId)) return Deny("coverage_only_fixture");
        if (!DemoAnalysisCatalog.IsAnalysisProfile(run.ProfileCatalogId)) return Deny("frozen_fixture_mismatch");
        if (run.State != SyntheticRunState.Scoring || run.CancelRequested || run.CoverageSummary is null) return Deny("coverage_not_ready");
        if (!DemoAnalysisCatalog.MatchesFrozenFixture(run)) return Deny("frozen_fixture_mismatch");
        var frozen = DemoAnalysisCatalog.Freeze(run.BaselineCatalogId, run.ProfileCatalogId);
        var plan = SyntheticAnalysisEngine.Plan(frozen);
        if (!run.Plan.ExpectedKeys.ToHashSet().SetEquals(plan.ExpectedKeys) ||
            !CoverageReconciler.Reconcile(run.Plan.ExpectedKeys, run.Results).IsComplete) return Deny("saved_plan_mismatch");
        var analyzed = SyntheticAnalysisEngine.Analyze(frozen, run.RunId, run.Results);
        if (!analyzed.Succeeded) return Deny("saved_result_mismatch");
        var analysis = analyzed.Analysis!;
        if (reviewedStates is not null)
        {
            if (!DemoAnalysisCatalog.IsReviewMaturityProfile(run.ProfileCatalogId) ||
                reviewSnapshotDigest is not { Length: 64 } || !reviewSnapshotDigest.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f') ||
                !reviewedStates.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(analysis.Groups.Select(group => group.RootCauseKey)))
                return Deny("invalid_review_snapshot");
            foreach (var original in analysis.Findings)
            {
                var state = reviewedStates[original.RootCauseKey];
                if (original.InitialDisposition == SyntheticInitialDisposition.AutoConfirmed ? state != ScoringFindingState.AutoConfirmed :
                    state is not (ScoringFindingState.Proposed or ScoringFindingState.Confirmed or ScoringFindingState.Rejected or ScoringFindingState.Deferred))
                    return Deny("invalid_review_snapshot");
            }
        }
        var findings = analysis.Findings.ToDictionary(finding => new CoverageKey(finding.ObjectId, finding.Provenance.RuleId));
        var units = analysis.Results.Select(result =>
        {
            var finding = findings.GetValueOrDefault(result.Unit.Key);
            return new ScoringUnit(result.Unit.Key, result.Unit.CategoryId, result.Unit.ObjectType, result.Unit.ModuleId,
                result.Unit.OutcomeIds, result.Coverage.State, result.Unit.Weight,
                finding is null ? null : Enum.Parse<ScoringSeverity>(finding.Severity.ToString()), ScoringDetectionMethod.Deterministic,
                finding is null ? null : reviewedStates?.GetValueOrDefault(finding.RootCauseKey) ?? Enum.Parse<ScoringFindingState>(finding.InitialDisposition.ToString()), 100m);
        }).ToArray();
        var profile = SyntheticAnalysisFixturePack.GetProfile(DemoAnalysisCatalog.AnalysisProfileId(run.ProfileCatalogId));
        var scoringProfile = new ScoringProfile(profile.Version,
            profile.Id == "synthetic-analysis-equal-v1" ? ScoringWeightMode.EqualAssessedCategories : ScoringWeightMode.ExplicitCategoryWeights,
            Array.AsReadOnly(profile.CategoryWeights.Select(item => new ScoringCategoryWeight(item.CategoryId, item.Weight)).ToArray()));
        var versions = new ScoringVersions(PilotHealthScorer.AlgorithmVersion, PilotHealthScorer.InputSchemaVersion,
            run.Plan.BaselineId, frozen.CatalogVersion, frozen.ProfileVersion, run.InputDigest,
            reviewedStates is null ? analysis.ContentDigest : SyntheticCanonicalDigest.Compute(new { analysis.ContentDigest, reviewSnapshotDigest }));
        var scored = PilotHealthScorer.Project(new(versions, scoringProfile, plan.ExpectedKeys, run.Results, Array.AsReadOnly(units), []));
        return scored.HasProjection ? new(null, new(analysis, scored.Projection!)) : Deny("scoring_input_denied");
    }
    private static SyntheticDemoAnalysisResponse Deny(string code) => new(code, null);
}
