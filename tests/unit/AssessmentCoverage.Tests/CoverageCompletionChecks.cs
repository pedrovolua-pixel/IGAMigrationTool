using AssessmentCoverage;

internal static class CoverageCompletionChecks
{
    internal static int Run()
    {
        var first = new CoverageKey("OBJECT-1", "AUTHORIZATION");
        var second = new CoverageKey("OBJECT-2", "AUDIT");
        var checks = 0;

        Check("pass, finding and not-applicable are complete", CoverageCompletionKind.Complete,
            [first, second, new CoverageKey("OBJECT-3", "SCHEMA")],
            [new CoverageItem(first, CoverageState.Pass),
             new CoverageItem(second, CoverageState.Finding),
             new CoverageItem(new CoverageKey("OBJECT-3", "SCHEMA"), CoverageState.NotApplicable,
                 "MODULE-ABSENT", "PLANNER")]);

        foreach (var state in new[]
                 {
                     CoverageState.NotAssessed, CoverageState.InsufficientEvidence,
                     CoverageState.Excluded, CoverageState.Inaccessible, CoverageState.Redacted,
                     CoverageState.Unsupported, CoverageState.Error
                 })
        {
            Check($"{state} is a gap", CoverageCompletionKind.CompleteWithGaps,
                [first], [new CoverageItem(first, state, "EXPLICIT-GAP", "ASSESSMENT")]);
        }

        var missing = CoverageCompletionProjector.Project([first, second],
            [new CoverageItem(first, CoverageState.Pass)]);
        Require(!missing.HasProjection && missing.Issues.Any(issue =>
            issue.Code == CoverageIssueCode.MissingResult), "missing result denies classification");
        checks++;

        var unexplained = CoverageCompletionProjector.Project([first],
            [new CoverageItem(first, CoverageState.Error)]);
        Require(!unexplained.HasProjection && unexplained.Issues.Any(issue =>
            issue.Code == CoverageIssueCode.MissingGapReason), "unexplained gap denies classification");
        checks++;

        var duplicate = CoverageCompletionProjector.Project([first],
            [new CoverageItem(first, CoverageState.Pass), new CoverageItem(first, CoverageState.Finding)]);
        Require(!duplicate.HasProjection && duplicate.Issues.Any(issue =>
            issue.Code == CoverageIssueCode.DuplicateResult), "duplicate result denies classification");
        checks++;

        var empty = CoverageCompletionProjector.Project([], []);
        Require(!empty.HasProjection && empty.Issues.Any(issue =>
            issue.Code == CoverageIssueCode.InvalidInput),
            "empty plan cannot be classified as a complete assessment");
        checks++;

        return checks;

        void Check(string name, CoverageCompletionKind expectedKind,
            CoverageKey[] expected, CoverageItem[] results)
        {
            var actual = CoverageCompletionProjector.Project(expected, results);
            Require(actual.HasProjection && actual.Kind == expectedKind && actual.Issues.Count == 0, name);
            checks++;
        }
    }

    private static void Require(bool condition, string name)
    {
        if (!condition) throw new Exception($"Coverage completion case failed: {name}.");
    }
}
