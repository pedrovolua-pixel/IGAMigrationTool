namespace CollectorSafety;

public sealed record PageBudgetPolicy(int MaximumPageSize, int MaximumRows, TimeSpan MaximumDuration);

public sealed record PageBudgetSnapshot(int RowsCompleted, TimeSpan Elapsed, bool CancellationRequested);

public enum PageBudgetDecision
{
    Permit,
    InvalidInput,
    Canceled,
    DurationLimit,
    RowLimit
}

public sealed record PageBudgetResult(PageBudgetDecision Decision, int PermittedPageSize);

/// <summary>
/// Computes the next bounded page size before a trusted query executor runs.
/// No SQL or timing mechanism is implemented here; the caller must enforce
/// cancellation and timeout during the actual command as well.
/// </summary>
public static class PageBudget
{
    public static PageBudgetResult Evaluate(PageBudgetPolicy? policy, PageBudgetSnapshot? snapshot)
    {
        if (policy is null || snapshot is null ||
            policy.MaximumPageSize <= 0 || policy.MaximumRows <= 0 ||
            policy.MaximumDuration <= TimeSpan.Zero ||
            snapshot.RowsCompleted < 0 || snapshot.Elapsed < TimeSpan.Zero)
        {
            return new PageBudgetResult(PageBudgetDecision.InvalidInput, 0);
        }

        if (snapshot.CancellationRequested)
        {
            return new PageBudgetResult(PageBudgetDecision.Canceled, 0);
        }

        if (snapshot.Elapsed >= policy.MaximumDuration)
        {
            return new PageBudgetResult(PageBudgetDecision.DurationLimit, 0);
        }

        if (snapshot.RowsCompleted >= policy.MaximumRows)
        {
            return new PageBudgetResult(PageBudgetDecision.RowLimit, 0);
        }

        return new PageBudgetResult(PageBudgetDecision.Permit,
            Math.Min(policy.MaximumPageSize, policy.MaximumRows - snapshot.RowsCompleted));
    }
}
