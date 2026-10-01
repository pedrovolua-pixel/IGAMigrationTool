namespace AssessmentCoverage;

public enum CoverageCompletionKind
{
    Complete,
    CompleteWithGaps
}

public sealed record CoverageCompletionResult(
    IReadOnlyList<CoverageIssue> Issues, CoverageCompletionKind? Kind)
{
    public bool HasProjection => Kind is not null;
}

/// <summary>
/// Classifies a reconciled terminal coverage plan by its explicit limitations.
/// This is a pure coverage projection, not an assessment run transition: it
/// does not decide failure, authorize a run or persist completion.
/// </summary>
public static class CoverageCompletionProjector
{
    public static CoverageCompletionResult Project(
        IReadOnlyCollection<CoverageKey>? expected,
        IReadOnlyCollection<CoverageItem>? results)
    {
        var limitations = CoverageLimitationProjector.Project(expected, results);
        if (!limitations.HasProjection)
        {
            return new CoverageCompletionResult(limitations.Issues, null);
        }
        if (expected!.Count == 0)
        {
            return new CoverageCompletionResult(
                [new CoverageIssue(CoverageIssueCode.InvalidInput, null)], null);
        }

        return new CoverageCompletionResult([], limitations.Limitations!.Count == 0
            ? CoverageCompletionKind.Complete
            : CoverageCompletionKind.CompleteWithGaps);
    }
}
