using AssessmentCoverage;

internal static class ExecutableCoverageChecks
{
    internal static int Run()
    {
        var count = 0;
        var keys = Enumerable.Range(0, 5)
            .Select(index => new CoverageKey($"OBJECT-{index}", "AUTHORIZATION"))
            .ToArray();
        var results = new[]
        {
            new CoverageItem(keys[0], CoverageState.Pass),
            new CoverageItem(keys[1], CoverageState.Finding),
            new CoverageItem(keys[2], CoverageState.NotApplicable, "MODULE-ABSENT", "PLANNER"),
            new CoverageItem(keys[3], CoverageState.Inaccessible, "PERMISSION-DENIED", "BASELINE"),
            new CoverageItem(keys[4], CoverageState.Error, "QUERY-FAILED", "COLLECTOR")
        };
        var measure = ExecutableCoverageProjector.Project(keys, results);
        Check("gaps remain in applicable denominator",
            measure.HasProjection && measure.Measure is
            { ExecutedUnits: 2, ApplicablePlannedUnits: 4, HasApplicableUnits: true });

        var noApplicable = ExecutableCoverageProjector.Project([keys[2]], [results[2]]);
        Check("no applicable units is explicit, not perfect coverage",
            noApplicable.Measure is
            {
                ExecutedUnits: 0, ApplicablePlannedUnits: 0,
                HasApplicableUnits: false
            });

        var missing = ExecutableCoverageProjector.Project(keys, results[..^1]);
        Check("missing result denies executable coverage",
            !missing.HasProjection && missing.Issues.Any(issue => issue.Code == CoverageIssueCode.MissingResult));

        var unexplained = ExecutableCoverageProjector.Project(keys,
            [.. results[..^1], results[^1] with { ReasonCode = null }]);
        Check("unexplained gap denies executable coverage",
            !unexplained.HasProjection && unexplained.Issues.Any(issue =>
                issue.Code == CoverageIssueCode.MissingGapReason));

        var duplicate = ExecutableCoverageProjector.Project(keys,
            [.. results[..^1], results[0]]);
        Check("duplicate result denies executable coverage",
            !duplicate.HasProjection && duplicate.Issues.Any(issue =>
                issue.Code == CoverageIssueCode.DuplicateResult));

        var largeKeys = Enumerable.Range(0, 100_000)
            .Select(index => new CoverageKey($"OBJECT-{index}", "AUTHORIZATION"))
            .ToArray();
        var largeResults = largeKeys.Select((key, index) => index % 10 == 0
            ? new CoverageItem(key, CoverageState.NotApplicable, "MODULE-ABSENT", "PLANNER")
            : new CoverageItem(key, CoverageState.Pass)).ToArray();
        var large = ExecutableCoverageProjector.Project(largeKeys, largeResults);
        Check("100,000-unit measure keeps exact counts",
            large.Measure is { ExecutedUnits: 90_000, ApplicablePlannedUnits: 90_000 });
        return count;

        void Check(string name, bool okay)
        {
            if (!okay) throw new Exception($"{name}: unexpected executable coverage.");
            count++;
        }
    }
}
