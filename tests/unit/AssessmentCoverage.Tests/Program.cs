using AssessmentCoverage;

var checks = 0;
var first = new CoverageKey("OBJECT-1", "AUTHORIZATION");
var second = new CoverageKey("OBJECT-2", "AUDIT");

Check("complete pass and finding", [], [first, second],
    [new CoverageItem(first, CoverageState.Pass), new CoverageItem(second, CoverageState.Finding)]);
Check("typed gap", [], [first],
    [new CoverageItem(first, CoverageState.Inaccessible, "PERMISSION-DENIED", "BASELINE-ADAPTER")]);
Check("missing result", [CoverageIssueCode.MissingResult], [first, second],
    [new CoverageItem(first, CoverageState.Pass)]);
Check("duplicate result", [CoverageIssueCode.DuplicateResult], [first],
    [new CoverageItem(first, CoverageState.Pass), new CoverageItem(first, CoverageState.Finding)]);
Check("unexpected result", [CoverageIssueCode.UnexpectedResult], [first],
    [new CoverageItem(first, CoverageState.Pass), new CoverageItem(second, CoverageState.Pass)]);
Check("duplicate expected key", [CoverageIssueCode.DuplicateExpectedKey], [first, first],
    [new CoverageItem(first, CoverageState.Pass)]);
Check("gap missing reason and stage", [CoverageIssueCode.MissingGapReason, CoverageIssueCode.MissingResponsibleStage],
    [first], [new CoverageItem(first, CoverageState.Redacted)]);
Check("opaque reason retained", [], [first],
    [new CoverageItem(first, CoverageState.Error, "UNAVAILABLE_BY_POLICY", "Baseline adapter")]);
Check("blank reason rejected", [CoverageIssueCode.MissingGapReason], [first],
    [new CoverageItem(first, CoverageState.Error, " ", "Baseline adapter")]);
Check("invalid state", [CoverageIssueCode.InvalidState], [first],
    [new CoverageItem(first, (CoverageState)999)]);
Check("invalid key", [CoverageIssueCode.InvalidKey],
    [new CoverageKey("", "AUDIT")], []);
Check("empty plan is complete", [], [], []);

var invalid = CoverageReconciler.Reconcile(null, []);
if (invalid.Issues.Count != 1 || invalid.Issues[0].Code != CoverageIssueCode.InvalidInput)
{
    throw new Exception("Null plan was not rejected.");
}
checks++;

Console.WriteLine($"{checks} assessment coverage reconciliation cases passed.");
Console.WriteLine($"{CoverageCountChecks.Run()} assessment coverage count cases passed.");
Console.WriteLine($"{CoverageLimitationChecks.Run()} assessment coverage limitation cases passed.");
Console.WriteLine($"{CoverageProgressChecks.Run()} assessment coverage progress cases passed.");
Console.WriteLine($"{ExecutableCoverageChecks.Run()} executable coverage cases passed.");
Console.WriteLine($"{CoverageCompletionChecks.Run()} coverage completion cases passed.");

void Check(string name, CoverageIssueCode[] expectedIssues,
    CoverageKey[] expected, CoverageItem[] results)
{
    var actual = CoverageReconciler.Reconcile(expected, results);
    var actualCodes = actual.Issues.Select(issue => issue.Code)
        .OrderBy(code => code).ToArray();
    var wantedCodes = expectedIssues.OrderBy(code => code).ToArray();
    if (!actualCodes.SequenceEqual(wantedCodes))
    {
        throw new Exception($"{name}: expected [{string.Join(',', wantedCodes)}], got [{string.Join(',', actualCodes)}]");
    }

    checks++;
}
