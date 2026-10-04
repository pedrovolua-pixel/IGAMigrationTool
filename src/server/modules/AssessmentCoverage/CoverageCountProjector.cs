namespace AssessmentCoverage;

public sealed record CoverageStateCount(CoverageState State, int Count);

public sealed record CoverageCountResult(
    IReadOnlyList<CoverageIssue> Issues,
    IReadOnlyList<CoverageStateCount>? Counts)
{
    public bool HasProjection => Counts is not null;
}

/// <summary>
/// Counts terminal coverage states only after expected keys and results
/// reconcile. This is an unweighted count, not a health or quality score.
/// </summary>
public static class CoverageCountProjector
{
    public static CoverageCountResult Project(
        IReadOnlyCollection<CoverageKey>? expected,
        IReadOnlyCollection<CoverageItem>? results)
    {
        var expectedSnapshot = expected?.ToArray();
        var resultSnapshot = results?.ToArray();
        var reconciliation = CoverageReconciler.Reconcile(expectedSnapshot, resultSnapshot);
        if (!reconciliation.IsComplete)
        {
            return new CoverageCountResult(reconciliation.Issues, null);
        }

        var counts = Enum.GetValues<CoverageState>()
            .ToDictionary(state => state, _ => 0);
        foreach (var result in resultSnapshot!)
        {
            counts[result.State]++;
        }

        return new CoverageCountResult([], Array.AsReadOnly(counts
            .Select(pair => new CoverageStateCount(pair.Key, pair.Value))
            .ToArray()));
    }
}
