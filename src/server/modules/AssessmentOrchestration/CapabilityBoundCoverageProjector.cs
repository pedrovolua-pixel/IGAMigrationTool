using AssessmentCoverage;

namespace AssessmentOrchestration;

public sealed record CapabilityBoundCoverageProjection(
    CapabilityVersionLock CapabilityLock,
    CoverageCompletionKind CompletionKind,
    IReadOnlyList<CoverageStateCount> Counts,
    ExecutableCoverageMeasure ExecutableCoverage,
    IReadOnlyList<CoverageLimitation> Limitations);

public sealed record CapabilityBoundCoverageResult(
    CapabilityLockIssue? CapabilityIssue,
    IReadOnlyList<CoverageIssue> CoverageIssues,
    CapabilityBoundCoverageProjection? Projection)
{
    public bool HasProjection => Projection is not null;
}

/// <summary>
/// Composes exact-version locking and terminal coverage projections for trusted
/// descriptors and an already authorized plan. It does not establish baseline
/// eligibility, build the inventory, authorize access, or transition a run.
/// </summary>
public static class CapabilityBoundCoverageProjector
{
    public static CapabilityBoundCoverageResult Project(
        CapabilitySnapshot? capability,
        BaselineCompatibility? baseline,
        IReadOnlyCollection<CoverageKey>? expected,
        IReadOnlyCollection<CoverageItem>? results)
    {
        var capabilityResult = CapabilityStartGuard.TryLock(capability, baseline);
        if (!capabilityResult.CanProceedToRemainingStartGates)
        {
            return new(capabilityResult.Issue, [], null);
        }

        var expectedSnapshot = expected?.ToArray();
        var resultSnapshot = results?.ToArray();
        var completion = CoverageCompletionProjector.Project(expectedSnapshot, resultSnapshot);
        if (!completion.HasProjection)
        {
            return new(null, Array.AsReadOnly(completion.Issues.ToArray()), null);
        }

        var counts = CoverageCountProjector.Project(expectedSnapshot, resultSnapshot);
        var executable = ExecutableCoverageProjector.Project(expectedSnapshot, resultSnapshot);
        var limitations = CoverageLimitationProjector.Project(expectedSnapshot, resultSnapshot);

        return new(null, [], new CapabilityBoundCoverageProjection(
            capabilityResult.Lock!, completion.Kind!.Value,
            Array.AsReadOnly(counts.Counts!.ToArray()), executable.Measure!,
            Array.AsReadOnly(limitations.Limitations!.ToArray())));
    }
}
