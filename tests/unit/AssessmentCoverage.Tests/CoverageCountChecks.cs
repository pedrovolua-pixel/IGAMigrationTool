using AssessmentCoverage;

internal static class CoverageCountChecks
{
    internal static int Run()
    {
        var first = new CoverageKey("OBJECT-1", "AUTHORIZATION");
        var second = new CoverageKey("OBJECT-2", "AUDIT");
        var passed = CoverageCountProjector.Project(
            [first, second],
            [new CoverageItem(first, CoverageState.Pass),
                new CoverageItem(second, CoverageState.Inaccessible, "PERMISSION-DENIED", "BASELINE-ADAPTER")]);
        Require(passed.HasProjection && passed.Issues.Count == 0 &&
                Count(passed, CoverageState.Pass) == 1 &&
                Count(passed, CoverageState.Inaccessible) == 1 &&
                Count(passed, CoverageState.Finding) == 0,
            "valid coverage counts");

        var incomplete = CoverageCountProjector.Project([first, second],
            [new CoverageItem(first, CoverageState.Pass)]);
        Require(!incomplete.HasProjection &&
                incomplete.Issues.Any(issue => issue.Code == CoverageIssueCode.MissingResult),
            "incomplete coverage denies projection");

        var empty = CoverageCountProjector.Project([], []);
        Require(empty.HasProjection && empty.Counts!.All(item => item.Count == 0),
            "empty eligible plan has zero counts");

        const int largeCount = 100_000;
        var keys = Enumerable.Range(0, largeCount)
            .Select(index => new CoverageKey($"OBJECT-{index}", "AUTHORIZATION"))
            .ToArray();
        var items = keys.Select(key => new CoverageItem(key, CoverageState.NotApplicable,
            "NOT-APPLICABLE", "INVENTORY")).ToArray();
        var large = CoverageCountProjector.Project(keys, items);
        Require(large.HasProjection && Count(large, CoverageState.NotApplicable) == largeCount,
            "100,000 terminal items count without a missing result");

        return 4;
    }

    private static int Count(CoverageCountResult result, CoverageState state) =>
        result.Counts!.Single(item => item.State == state).Count;

    private static void Require(bool condition, string name)
    {
        if (!condition)
        {
            throw new Exception($"Coverage count case failed: {name}.");
        }
    }
}
