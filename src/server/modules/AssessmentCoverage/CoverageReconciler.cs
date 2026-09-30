namespace AssessmentCoverage;

public enum CoverageState
{
    Pass,
    Finding,
    NotApplicable,
    NotAssessed,
    InsufficientEvidence,
    Excluded,
    Inaccessible,
    Redacted,
    Unsupported,
    Error
}

public sealed record CoverageKey(string InventoryId, string EvidenceCategory);

public sealed record CoverageItem(
    CoverageKey Key,
    CoverageState State,
    string? ReasonCode = null,
    string? ResponsibleStage = null,
    string? EvidenceReference = null);

public enum CoverageIssueCode
{
    InvalidInput,
    InvalidKey,
    DuplicateExpectedKey,
    DuplicateResult,
    UnexpectedResult,
    MissingResult,
    InvalidState,
    MissingGapReason,
    MissingResponsibleStage
}

public sealed record CoverageIssue(CoverageIssueCode Code, CoverageKey? Key);

public sealed record CoverageReconciliationResult(IReadOnlyList<CoverageIssue> Issues)
{
    public bool IsComplete => Issues.Count == 0;
}

/// <summary>
/// Checks that an already authorized run plan has exactly one terminal result
/// per expected inventory/category key. It does not construct the plan, score
/// results, read evidence, or activate an assessment.
/// </summary>
public static class CoverageReconciler
{
    public static CoverageReconciliationResult Reconcile(
        IReadOnlyCollection<CoverageKey>? expected,
        IReadOnlyCollection<CoverageItem>? results) => Validate(expected, results, true);

    internal static IReadOnlyList<CoverageIssue> ValidatePartial(
        IReadOnlyCollection<CoverageKey>? expected,
        IReadOnlyCollection<CoverageItem>? results) => Validate(expected, results, false).Issues;

    private static CoverageReconciliationResult Validate(
        IReadOnlyCollection<CoverageKey>? expected,
        IReadOnlyCollection<CoverageItem>? results,
        bool requireAll)
    {
        if (expected is null || results is null)
        {
            return new CoverageReconciliationResult([new CoverageIssue(CoverageIssueCode.InvalidInput, null)]);
        }

        var issues = new List<CoverageIssue>();
        var expectedKeys = new HashSet<CoverageKey>();
        foreach (var key in expected)
        {
            if (!ValidKey(key))
            {
                issues.Add(new CoverageIssue(CoverageIssueCode.InvalidKey, key));
            }
            else if (!expectedKeys.Add(key))
            {
                issues.Add(new CoverageIssue(CoverageIssueCode.DuplicateExpectedKey, key));
            }
        }

        var seen = new HashSet<CoverageKey>();
        foreach (var item in results)
        {
            if (item is null || !ValidKey(item.Key))
            {
                issues.Add(new CoverageIssue(CoverageIssueCode.InvalidKey, item?.Key));
                continue;
            }

            if (!expectedKeys.Contains(item.Key))
            {
                issues.Add(new CoverageIssue(CoverageIssueCode.UnexpectedResult, item.Key));
            }

            if (!seen.Add(item.Key))
            {
                issues.Add(new CoverageIssue(CoverageIssueCode.DuplicateResult, item.Key));
            }

            if (!Enum.IsDefined(item.State))
            {
                issues.Add(new CoverageIssue(CoverageIssueCode.InvalidState, item.Key));
                continue;
            }

            if (item.State is CoverageState.Pass or CoverageState.Finding)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.ReasonCode))
            {
                issues.Add(new CoverageIssue(CoverageIssueCode.MissingGapReason, item.Key));
            }

            if (string.IsNullOrWhiteSpace(item.ResponsibleStage))
            {
                issues.Add(new CoverageIssue(CoverageIssueCode.MissingResponsibleStage, item.Key));
            }
        }

        if (requireAll)
        {
            foreach (var key in expectedKeys)
            {
                if (!seen.Contains(key))
                {
                    issues.Add(new CoverageIssue(CoverageIssueCode.MissingResult, key));
                }
            }
        }

        return new CoverageReconciliationResult(issues);
    }

    private static bool ValidKey(CoverageKey? key) =>
        key is not null &&
        !string.IsNullOrWhiteSpace(key.InventoryId) &&
        !string.IsNullOrWhiteSpace(key.EvidenceCategory);
}
