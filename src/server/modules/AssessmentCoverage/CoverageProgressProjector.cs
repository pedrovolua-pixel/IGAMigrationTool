namespace AssessmentCoverage;

public sealed record CoverageProgress(
    int PlannedUnits, int TerminalUnits, int RemainingUnits,
    IReadOnlyList<CoverageStateCount> TerminalStateCounts)
{
    public bool AllTerminal => RemainingUnits == 0;
}

public sealed record CoverageProgressResult(
    IReadOnlyList<CoverageIssue> Issues, CoverageProgress? Progress)
{
    public bool HasProjection => Progress is not null;
}

/// <summary>
/// Reports terminal coverage progress against a trusted plan. Missing results
/// are expected while a run is in progress; every other reconciliation issue
/// blocks the projection. This does not classify or complete an assessment run.
/// </summary>
public static class CoverageProgressProjector
{
    public static CoverageProgressResult Project(
        IReadOnlyCollection<CoverageKey>? expected,
        IReadOnlyCollection<CoverageItem>? results)
    {
        var expectedSnapshot = expected?.ToArray();
        var resultSnapshot = results?.ToArray();
        var issues = CoverageReconciler.ValidatePartial(expectedSnapshot, resultSnapshot);
        if (issues.Count != 0)
        {
            return new CoverageProgressResult(issues, null);
        }

        var counts = Enum.GetValues<CoverageState>()
            .ToDictionary(state => state, _ => 0);
        foreach (var result in resultSnapshot!)
        {
            counts[result.State]++;
        }

        var progress = new CoverageProgress(expectedSnapshot!.Length, resultSnapshot.Length,
            expectedSnapshot.Length - resultSnapshot.Length,
            Array.AsReadOnly(counts.Select(pair =>
                new CoverageStateCount(pair.Key, pair.Value)).ToArray()));
        return new CoverageProgressResult([], progress);
    }
}
