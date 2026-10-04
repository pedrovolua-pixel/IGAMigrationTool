namespace AssessmentCoverage;

public sealed record CoverageLimitation(
    CoverageState State, string ReasonCode, string ResponsibleStage, int Count);

public sealed record CoverageLimitationResult(
    IReadOnlyList<CoverageIssue> Issues,
    IReadOnlyList<CoverageLimitation>? Limitations)
{
    public bool HasProjection => Limitations is not null;
}

/// <summary>
/// Summarizes explicit coverage limitations from a reconciled plan. It does not
/// read evidence, infer a healthy result, or calculate a health or quality score.
/// </summary>
public static class CoverageLimitationProjector
{
    public static CoverageLimitationResult Project(
        IReadOnlyCollection<CoverageKey>? expected,
        IReadOnlyCollection<CoverageItem>? results)
    {
        var expectedSnapshot = expected?.ToArray();
        var resultSnapshot = results?.ToArray();
        var reconciliation = CoverageReconciler.Reconcile(expectedSnapshot, resultSnapshot);
        if (!reconciliation.IsComplete)
        {
            return new CoverageLimitationResult(reconciliation.Issues, null);
        }

        var limitations = resultSnapshot!
            .Where(item => IsLimitation(item.State))
            .GroupBy(item => new { item.State, item.ReasonCode, item.ResponsibleStage })
            .Select(group => new CoverageLimitation(group.Key.State,
                group.Key.ReasonCode!, group.Key.ResponsibleStage!, group.Count()))
            .OrderBy(item => item.State)
            .ThenBy(item => item.ReasonCode, StringComparer.Ordinal)
            .ThenBy(item => item.ResponsibleStage, StringComparer.Ordinal)
            .ToArray();

        return new CoverageLimitationResult([], Array.AsReadOnly(limitations));
    }

    private static bool IsLimitation(CoverageState state) => state is
        CoverageState.NotAssessed or CoverageState.InsufficientEvidence or
        CoverageState.Excluded or CoverageState.Inaccessible or CoverageState.Redacted or
        CoverageState.Unsupported or CoverageState.Error;
}
