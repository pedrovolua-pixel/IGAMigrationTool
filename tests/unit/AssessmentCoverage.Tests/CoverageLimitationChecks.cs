using AssessmentCoverage;

internal static class CoverageLimitationChecks
{
    internal static int Run()
    {
        var keys = Enumerable.Range(0, 6)
            .Select(index => new CoverageKey($"OBJECT-{index}", "AUTHORIZATION"))
            .ToArray();
        var results = new CoverageItem[]
        {
            new(keys[0], CoverageState.Pass),
            new(keys[1], CoverageState.Finding),
            new(keys[2], CoverageState.NotApplicable, "MODULE-ABSENT", "PLANNER"),
            new(keys[3], CoverageState.Inaccessible, "PERMISSION-DENIED", "BASELINE"),
            new(keys[4], CoverageState.Inaccessible, "PERMISSION-DENIED", "BASELINE"),
            new(keys[5], CoverageState.Unsupported, "MODULE-NOT-VALIDATED", "PLANNER")
        };
        var projected = CoverageLimitationProjector.Project(keys, results);
        Require(projected.HasProjection && projected.Limitations!.SequenceEqual(
            [new CoverageLimitation(CoverageState.Inaccessible, "PERMISSION-DENIED", "BASELINE", 2),
             new CoverageLimitation(CoverageState.Unsupported, "MODULE-NOT-VALIDATED", "PLANNER", 1)]),
            "only limitations are grouped by reason and stage");

        var missing = CoverageLimitationProjector.Project(keys, results[..^1]);
        Require(!missing.HasProjection && missing.Limitations is null &&
                missing.Issues.Any(issue => issue.Code == CoverageIssueCode.MissingResult),
            "missing terminal result denies projection");

        var malformed = CoverageLimitationProjector.Project([keys[3]],
            [new CoverageItem(keys[3], CoverageState.Inaccessible, " ", "BASELINE")]);
        Require(!malformed.HasProjection && malformed.Issues.Any(issue =>
                issue.Code == CoverageIssueCode.MissingGapReason),
            "unexplained limitation denies projection");

        var clear = CoverageLimitationProjector.Project([keys[0]], [results[0]]);
        Require(clear.HasProjection && clear.Limitations!.Count == 0,
            "reconciled healthy result has no limitations");

        var limitationStates = new[]
        {
            CoverageState.NotAssessed, CoverageState.InsufficientEvidence,
            CoverageState.Excluded, CoverageState.Inaccessible, CoverageState.Redacted,
            CoverageState.Unsupported, CoverageState.Error
        };
        var stateKeys = limitationStates.Select((_, index) =>
            new CoverageKey($"GAP-{index}", "AUTHORIZATION")).ToArray();
        var allGaps = CoverageLimitationProjector.Project(stateKeys,
            limitationStates.Select((state, index) =>
                new CoverageItem(stateKeys[index], state, "EXPLICIT-LIMITATION", "ASSESSMENT")).ToArray());
        Require(allGaps.HasProjection && allGaps.Limitations!.Count == limitationStates.Length &&
                allGaps.Limitations.All(item => item.Count == 1),
            "every approved limitation state is represented");

        return 5;
    }

    private static void Require(bool condition, string name)
    {
        if (!condition)
        {
            throw new Exception($"Coverage limitation case failed: {name}.");
        }
    }
}
