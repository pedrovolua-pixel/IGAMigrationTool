namespace AssessmentCoverage;

public sealed record ExecutableCoverageMeasure(int ExecutedUnits, int ApplicablePlannedUnits)
{
    public bool HasApplicableUnits => ApplicablePlannedUnits > 0;
}

public sealed record ExecutableCoverageResult(
    IReadOnlyList<CoverageIssue> Issues, ExecutableCoverageMeasure? Measure)
{
    public bool HasProjection => Measure is not null;
}

/// <summary>
/// The executable-coverage numerator and denominator in pilot-quality-v1.
/// Only pass/finding units count as executed. Explicit not-applicable units
/// leave the denominator; every other terminal gap remains in it. This is
/// neither a health score nor an assessment-run completion decision.
/// </summary>
public static class ExecutableCoverageProjector
{
    public static ExecutableCoverageResult Project(
        IReadOnlyCollection<CoverageKey>? expected,
        IReadOnlyCollection<CoverageItem>? results)
    {
        var expectedSnapshot = expected?.ToArray();
        var resultSnapshot = results?.ToArray();
        var reconciliation = CoverageReconciler.Reconcile(expectedSnapshot, resultSnapshot);
        if (!reconciliation.IsComplete)
        {
            return new ExecutableCoverageResult(reconciliation.Issues, null);
        }

        var executed = 0;
        var notApplicable = 0;
        foreach (var item in resultSnapshot!)
        {
            if (item.State is CoverageState.Pass or CoverageState.Finding) executed++;
            if (item.State == CoverageState.NotApplicable) notApplicable++;
        }

        return new ExecutableCoverageResult([], new ExecutableCoverageMeasure(
            executed, expectedSnapshot!.Length - notApplicable));
    }
}
