using AssessmentCoverage;

internal static class CoverageProgressChecks
{
    internal static int Run()
    {
        var first = new CoverageKey("OBJECT-1", "AUTHORIZATION");
        var second = new CoverageKey("OBJECT-2", "AUDIT");
        var pass = new CoverageItem(first, CoverageState.Pass);
        var gap = new CoverageItem(second, CoverageState.Inaccessible,
            "PERMISSION-DENIED", "BASELINE");

        var partial = CoverageProgressProjector.Project([first, second], [pass]);
        Require(partial.HasProjection && partial.Progress is
        { PlannedUnits: 2, TerminalUnits: 1, RemainingUnits: 1, AllTerminal: false } &&
                Count(partial, CoverageState.Pass) == 1 &&
                Count(partial, CoverageState.Inaccessible) == 0,
            "partial run reports only validated terminal units");

        var complete = CoverageProgressProjector.Project([first, second], [pass, gap]);
        Require(complete.HasProjection && complete.Progress is
        { PlannedUnits: 2, TerminalUnits: 2, RemainingUnits: 0, AllTerminal: true } &&
                Count(complete, CoverageState.Inaccessible) == 1,
            "complete terminal coverage retains gap count");

        var duplicate = CoverageProgressProjector.Project([first, second], [pass, pass]);
        Require(!duplicate.HasProjection && duplicate.Issues.Any(issue =>
                issue.Code == CoverageIssueCode.DuplicateResult),
            "duplicate result blocks progress");

        var unexpected = CoverageProgressProjector.Project([first], [gap]);
        Require(!unexpected.HasProjection && unexpected.Issues.Any(issue =>
                issue.Code == CoverageIssueCode.UnexpectedResult),
            "unexpected result blocks progress");

        var unexplained = CoverageProgressProjector.Project([second],
            [gap with { ReasonCode = null }]);
        Require(!unexplained.HasProjection && unexplained.Issues.Any(issue =>
                issue.Code == CoverageIssueCode.MissingGapReason),
            "unexplained gap blocks progress");

        var largePlan = Enumerable.Range(0, 100_000)
            .Select(index => new CoverageKey($"OBJECT-{index}", "AUTHORIZATION"))
            .ToArray();
        var notStarted = CoverageProgressProjector.Project(largePlan, []);
        Require(notStarted.HasProjection && notStarted.Issues.Count == 0 &&
                notStarted.Progress is
                { PlannedUnits: 100_000, TerminalUnits: 0, RemainingUnits: 100_000, AllTerminal: false },
            "large plan reports zero progress without materializing missing-key issues");

        return 6;
    }

    private static int Count(CoverageProgressResult result, CoverageState state) =>
        result.Progress!.TerminalStateCounts.Single(item => item.State == state).Count;

    private static void Require(bool condition, string name)
    {
        if (!condition)
        {
            throw new Exception($"Coverage progress case failed: {name}.");
        }
    }
}
