using CollectorSafety;

internal static class PageBudgetChecks
{
    internal static int Run()
    {
        var policy = new PageBudgetPolicy(100, 250, TimeSpan.FromMinutes(5));
        var snapshot = new PageBudgetSnapshot(0, TimeSpan.Zero, false);
        var count = 0;

        Check("first page", PageBudgetDecision.Permit, 100, policy, snapshot);
        Check("partial final page", PageBudgetDecision.Permit, 50, policy,
            snapshot with { RowsCompleted = 200 });
        Check("row cap", PageBudgetDecision.RowLimit, 0, policy,
            snapshot with { RowsCompleted = 250 });
        Check("duration cap", PageBudgetDecision.DurationLimit, 0, policy,
            snapshot with { Elapsed = TimeSpan.FromMinutes(5) });
        Check("cancellation", PageBudgetDecision.Canceled, 0, policy,
            snapshot with { CancellationRequested = true });
        Check("negative completed rows", PageBudgetDecision.InvalidInput, 0, policy,
            snapshot with { RowsCompleted = -1 });
        Check("negative elapsed", PageBudgetDecision.InvalidInput, 0, policy,
            snapshot with { Elapsed = TimeSpan.FromTicks(-1) });
        Check("zero page size", PageBudgetDecision.InvalidInput, 0,
            policy with { MaximumPageSize = 0 }, snapshot);
        Check("zero row cap", PageBudgetDecision.InvalidInput, 0,
            policy with { MaximumRows = 0 }, snapshot);
        Check("zero duration", PageBudgetDecision.InvalidInput, 0,
            policy with { MaximumDuration = TimeSpan.Zero }, snapshot);
        Check("missing policy", PageBudgetDecision.InvalidInput, 0, null, snapshot);
        Check("missing snapshot", PageBudgetDecision.InvalidInput, 0, policy, null);

        return count;

        void Check(string name, PageBudgetDecision decision, int size,
            PageBudgetPolicy? candidatePolicy, PageBudgetSnapshot? candidateSnapshot)
        {
            var actual = PageBudget.Evaluate(candidatePolicy, candidateSnapshot);
            if (actual.Decision != decision || actual.PermittedPageSize != size)
            {
                throw new Exception($"{name}: expected {decision}/{size}, got {actual.Decision}/{actual.PermittedPageSize}.");
            }

            count++;
        }
    }
}
