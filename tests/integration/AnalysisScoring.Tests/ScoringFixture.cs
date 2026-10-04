using AssessmentCoverage;
using AssessmentScoring;

internal static class ScoringFixture
{
    internal static ScoringUnit Pass(string id, string category = "a", decimal weight = 1m) =>
        new(new(id, "independent-rule"), category, "synthetic-object-type", "synthetic-module",
            [], CoverageState.Pass, weight, null, ScoringDetectionMethod.Deterministic, null, 100m);

    internal static ScoringUnit Finding(string id, ScoringSeverity severity, ScoringFindingState state = ScoringFindingState.Confirmed,
        string category = "a", decimal weight = 1m, ScoringDetectionMethod method = ScoringDetectionMethod.Deterministic, decimal confidence = 100m) =>
        new(new(id, "independent-rule"), category, "synthetic-object-type", "synthetic-module",
            [], CoverageState.Finding, weight, severity, method, state, confidence);

    internal static ScoringUnit Gap(string id, CoverageState state) => Pass(id) with { CoverageState = state };

    internal static ScoringInput Input(params ScoringUnit[] units) => new(
        new("pilot-health-v1", "synthetic-scoring-input-v1", "independent-baseline-v1", "independent-catalog-v1", "independent-profile-v1", new('a', 64), new('b', 64)),
        new("independent-profile-v1", ScoringWeightMode.EqualAssessedCategories,
            units.Select(unit => unit.CategoryId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(category => new ScoringCategoryWeight(category, 1m)).ToArray()),
        units.Select(unit => unit.Key).ToArray(),
        units.Select(unit => unit.CoverageState is CoverageState.Pass or CoverageState.Finding
            ? new CoverageItem(unit.Key, unit.CoverageState)
            : new CoverageItem(unit.Key, unit.CoverageState, "synthetic-explained-gap", "independent-verification")).ToArray(),
        units, []);
}
