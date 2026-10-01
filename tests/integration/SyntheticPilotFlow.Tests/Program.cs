using System.Security.Cryptography;
using System.Text.Json;
using AssessmentCoverage;
using AssessmentOrchestration;

// Versioned synthetic composition of pure primitives. No baseline adapter,
// inventory planner, authorization, eligibility or durable run is exercised.
const string fixtureVersion = "synthetic-pilot-flow-v1";
var modules = new[] { new ModuleVersion("QBM", "synthetic-module-v1"), new ModuleVersion("QER", "synthetic-module-v2") };
var capability = new CapabilitySnapshot("synthetic-matrix-v1", CapabilityLifecycleState.FixtureVerified,
    "10.synthetic.1", "synthetic-schema-v1", "synthetic-hotfix-digest", "synthetic-sql-v1", 160,
    modules, "synthetic-pack-v1", "synthetic-normalized-v1", "synthetic-rules-v1");
var baseline = new BaselineCompatibility(capability.ProductBuild, capability.DatabaseSchemaBuild,
    capability.HotfixSetDigest, capability.SqlServerBuild, capability.CompatibilityLevel,
    modules.Reverse().ToArray(), capability.QueryPackVersion, capability.NormalizationSchemaVersion);
var planned = Enumerable.Range(0, 10).Select(index => new CoverageKey($"SYNTHETIC-OBJECT-{index}", "SYNTHETIC-CATEGORY")).ToArray();
var results = new[]
{
    new CoverageItem(planned[0], CoverageState.Pass),
    new CoverageItem(planned[1], CoverageState.Finding),
    new CoverageItem(planned[2], CoverageState.NotApplicable, "MODULE-NOT-INSTALLED", "SYNTHETIC-PLANNER"),
    new CoverageItem(planned[3], CoverageState.NotAssessed, "NOT-EXECUTED", "SYNTHETIC-WORKER"),
    new CoverageItem(planned[4], CoverageState.InsufficientEvidence, "MISSING-REFERENCE", "SYNTHETIC-ADAPTER"),
    new CoverageItem(planned[5], CoverageState.Excluded, "POLICY-EXCLUSION", "SYNTHETIC-POLICY"),
    new CoverageItem(planned[6], CoverageState.Inaccessible, "READ-DENIED", "SYNTHETIC-ADAPTER"),
    new CoverageItem(planned[7], CoverageState.Redacted, "FIELD-REDACTED", "SYNTHETIC-POLICY"),
    new CoverageItem(planned[8], CoverageState.Unsupported, "SEMANTIC-ANALYSIS-DEFERRED", "SYNTHETIC-CATALOG"),
    new CoverageItem(planned[9], CoverageState.Error, "RULE-FAILED", "SYNTHETIC-WORKER")
};
var checks = 0;

Case("SYN-PILOT-001", "FR-HAS-23;TP-HAS-001", new { capability, baseline, planned, results }, () =>
{
    var locked = Lock(capability, baseline);
    var bound = Bound(planned, results);
    var reordered = Lock(capability with { Modules = modules.Reverse().ToArray() }, baseline);
    Require(locked.StateAtLock == CapabilityLifecycleState.FixtureVerified, "lifecycle remains fixture verified");
    Require(locked.LockDigest == reordered.LockDigest, "lock independent of module order");
    Require(locked.Modules.Select(module => module.Id).SequenceEqual(new[] { "QBM", "QER" }), "canonical module order");
    var original = modules[0];
    modules[0] = original with { Version = "changed-after-lock" };
    Require(locked.Modules[0] == original, "lock retains immutable module snapshot");
    Require(bound.CapabilityLock.Modules[0] == original && bound.CapabilityLock.StateAtLock == CapabilityLifecycleState.FixtureVerified,
        "combined projection retains fixture lifecycle and immutable module snapshot");
    modules[0] = original;
    Require(Lock(capability with { RuleCatalogVersion = "synthetic-rules-v2" }, baseline).LockDigest != locked.LockDigest,
        "rule catalog change creates different lock");
});

Case("SYN-PILOT-002", "FR-HAS-2/8/18;AC-HAS-1/7;TP-HAS-001/007", new { capability, baseline, planned, results }, () =>
{
    _ = Lock(capability, baseline);
    Require(CoverageReconciler.Reconcile(planned, results).IsComplete, "every terminal state reconciles");
    var progress = CoverageProgressProjector.Project(planned, results).Progress;
    Require(progress is { PlannedUnits: 10, TerminalUnits: 10, RemainingUnits: 0, AllTerminal: true }, "all ten units terminal");
    var counts = CoverageCountProjector.Project(planned, results).Counts;
    Require(counts is { Count: 10 } && counts.All(count => count.Count == 1), "one of every terminal state");
    var measure = ExecutableCoverageProjector.Project(planned, results).Measure;
    Require(measure is { ExecutedUnits: 2, ApplicablePlannedUnits: 9, HasApplicableUnits: true }, "gap units retained in denominator");
    var limitations = CoverageLimitationProjector.Project(planned, results).Limitations;
    var expectedLimitations = new[]
    {
        new CoverageLimitation(CoverageState.NotAssessed, "NOT-EXECUTED", "SYNTHETIC-WORKER", 1),
        new CoverageLimitation(CoverageState.InsufficientEvidence, "MISSING-REFERENCE", "SYNTHETIC-ADAPTER", 1),
        new CoverageLimitation(CoverageState.Excluded, "POLICY-EXCLUSION", "SYNTHETIC-POLICY", 1),
        new CoverageLimitation(CoverageState.Inaccessible, "READ-DENIED", "SYNTHETIC-ADAPTER", 1),
        new CoverageLimitation(CoverageState.Redacted, "FIELD-REDACTED", "SYNTHETIC-POLICY", 1),
        new CoverageLimitation(CoverageState.Unsupported, "SEMANTIC-ANALYSIS-DEFERRED", "SYNTHETIC-CATALOG", 1),
        new CoverageLimitation(CoverageState.Error, "RULE-FAILED", "SYNTHETIC-WORKER", 1)
    };
    Require(limitations is not null && limitations.SequenceEqual(expectedLimitations), "seven explicit reason/stage limitations");
    Require(CoverageCompletionProjector.Project(planned, results).Kind == CoverageCompletionKind.CompleteWithGaps,
        "terminal gap plan classified complete with gaps");
    var bound = Bound(planned, results);
    Require(bound.CompletionKind == CoverageCompletionKind.CompleteWithGaps, "combined gap classification");
    Require(bound.Counts.Count == 10 && bound.Counts.All(count => count.Count == 1), "combined one of every state");
    Require(bound.ExecutableCoverage == new ExecutableCoverageMeasure(2, 9), "combined exact executable measure");
    Require(bound.Limitations.SequenceEqual(expectedLimitations), "combined exact limitation metadata");
});

Case("SYN-PILOT-003", "FR-HAS-2;AC-HAS-1;TP-HAS-001", new { capability, baseline, planned, PartialResults = results[..2] }, () =>
{
    _ = Lock(capability, baseline);
    var partial = results[..2];
    var progress = CoverageProgressProjector.Project(planned, partial).Progress;
    Require(progress is { PlannedUnits: 10, TerminalUnits: 2, RemainingUnits: 8, AllTerminal: false }, "missing results remain outstanding");
    Require(progress!.TerminalStateCounts.Single(count => count.State == CoverageState.Pass).Count == 1 &&
        progress.TerminalStateCounts.Single(count => count.State == CoverageState.Finding).Count == 1,
        "partial pass and finding counted");
    DenyTerminal(planned, partial, CoverageIssueCode.MissingResult);
});

Case("SYN-PILOT-004", "FR-HAS-2/18;AC-HAS-1/7;TP-HAS-001/007", new { capability, baseline, Planned = planned[..3], Results = results[..3] }, () =>
{
    _ = Lock(capability, baseline);
    Require(CoverageCompletionProjector.Project(planned[..3], results[..3]).Kind == CoverageCompletionKind.Complete,
        "pass finding and explained not applicable have no gap");
    Require(ExecutableCoverageProjector.Project(planned[..3], results[..3]).Measure is
    { ExecutedUnits: 2, ApplicablePlannedUnits: 2 }, "not applicable removes exactly one denominator unit");
    Require(ExecutableCoverageProjector.Project([planned[2]], [results[2]]).Measure is
    { ExecutedUnits: 0, ApplicablePlannedUnits: 0, HasApplicableUnits: false }, "all not applicable is unavailable");
    Require(!CoverageCompletionProjector.Project([], []).HasProjection, "empty plan cannot classify completion");
    var bound = Bound(planned[..3], results[..3]);
    Require(bound.CompletionKind == CoverageCompletionKind.Complete && bound.Limitations.Count == 0 &&
        bound.ExecutableCoverage == new ExecutableCoverageMeasure(2, 2), "combined complete healthy plan");
    var unavailable = Bound([planned[2]], [results[2]]);
    Require(unavailable.ExecutableCoverage is { ExecutedUnits: 0, ApplicablePlannedUnits: 0, HasApplicableUnits: false },
        "combined all not applicable measure unavailable");
    var empty = CapabilityBoundCoverageProjector.Project(capability, baseline, [], []);
    Require(!empty.HasProjection && empty.Projection is null && empty.CapabilityIssue is null &&
        empty.CoverageIssues.Any(issue => issue.Code == CoverageIssueCode.InvalidInput), "combined empty plan denied");
});

var deniedCapabilities = new[]
{
    (capability with { State = CapabilityLifecycleState.Suspended }, CapabilityLockIssue.Suspended),
    (capability with { State = CapabilityLifecycleState.Unsupported }, CapabilityLockIssue.Unsupported),
    (capability with { DatabaseSchemaBuild = "synthetic-schema-v2" }, CapabilityLockIssue.ExactVersionMismatch),
    (capability with { Modules = [new ModuleVersion("QBM", "synthetic-module-v1")] }, CapabilityLockIssue.ModuleInventoryMismatch)
};
Case("SYN-PILOT-005", "FR-HAS-23;AC-HAS-1;TP-HAS-001", new { deniedCapabilities, baseline }, () =>
{
    foreach (var (candidate, expectedIssue) in deniedCapabilities)
    {
        var denied = CapabilityStartGuard.TryLock(candidate, baseline);
        Require(!denied.CanProceedToRemainingStartGates && denied.Lock is null && denied.Issue == expectedIssue,
            "incompatible lock denied with typed issue");
        var bound = CapabilityBoundCoverageProjector.Project(candidate, baseline, null, null);
        Require(!bound.HasProjection && bound.Projection is null && bound.CapabilityIssue == expectedIssue && bound.CoverageIssues.Count == 0,
            "combined capability denial precedes coverage and exposes no summary");
    }
});

var invalidResults = new[]
{
    (results.Append(results[0]).ToArray(), CoverageIssueCode.DuplicateResult),
    (results.Append(new CoverageItem(new CoverageKey("UNPLANNED-SYNTHETIC-OBJECT", "SYNTHETIC-CATEGORY"), CoverageState.Pass)).ToArray(), CoverageIssueCode.UnexpectedResult),
    (results.Select(item => item.State == CoverageState.Error ? item with { ReasonCode = null } : item).ToArray(), CoverageIssueCode.MissingGapReason),
    (results.Select(item => item.State == CoverageState.Error ? item with { ResponsibleStage = null } : item).ToArray(), CoverageIssueCode.MissingResponsibleStage)
};
Case("SYN-PILOT-006", "FR-HAS-2/18;AC-HAS-1/7;TP-HAS-001/007", new { capability, baseline, planned, invalidResults }, () =>
{
    _ = Lock(capability, baseline);
    foreach (var (invalid, issue) in invalidResults)
    {
        var progress = CoverageProgressProjector.Project(planned, invalid);
        Require(!progress.HasProjection && progress.Issues.Any(found => found.Code == issue), "malformed partial projection denied");
        DenyTerminal(planned, invalid, issue);
    }
});

Console.WriteLine($"{checks} synthetic pilot composition assertions passed across 6 versioned fixtures.");
Console.WriteLine("Scope: pure capability/coverage composition only; eligibility, authorization, durable execution and live gates NOT VERIFIED.");

SyntheticBaselineFlowCases.Run();

CapabilityVersionLock Lock(CapabilitySnapshot candidate, BaselineCompatibility source)
{
    var result = CapabilityStartGuard.TryLock(candidate, source);
    Require(result.CanProceedToRemainingStartGates && result.Issue is null && result.Lock is not null, "synthetic exact lock available");
    return result.Lock!;
}

CapabilityBoundCoverageProjection Bound(CoverageKey[] expected, CoverageItem[] actual)
{
    var result = CapabilityBoundCoverageProjector.Project(capability, baseline, expected, actual);
    Require(result.HasProjection && result.Projection is not null && result.CapabilityIssue is null && result.CoverageIssues.Count == 0,
        "combined synthetic projection available");
    return result.Projection!;
}

void DenyTerminal(CoverageKey[] expected, CoverageItem[] actual, CoverageIssueCode issue)
{
    var reconciliation = CoverageReconciler.Reconcile(expected, actual);
    var counts = CoverageCountProjector.Project(expected, actual);
    var limitations = CoverageLimitationProjector.Project(expected, actual);
    var executable = ExecutableCoverageProjector.Project(expected, actual);
    var completion = CoverageCompletionProjector.Project(expected, actual);
    Require(!reconciliation.IsComplete && reconciliation.Issues.Any(found => found.Code == issue), "terminal reconciliation denied");
    Require(!counts.HasProjection && counts.Issues.Any(found => found.Code == issue), "terminal counts denied");
    Require(!limitations.HasProjection && limitations.Issues.Any(found => found.Code == issue), "terminal limitations denied");
    Require(!executable.HasProjection && executable.Issues.Any(found => found.Code == issue), "terminal measure denied");
    Require(!completion.HasProjection && completion.Issues.Any(found => found.Code == issue), "terminal classification denied");
    var bound = CapabilityBoundCoverageProjector.Project(capability, baseline, expected, actual);
    Require(!bound.HasProjection && bound.Projection is null && bound.CapabilityIssue is null &&
        bound.CoverageIssues.Any(found => found.Code == issue), "combined invalid coverage exposes no summary");
}

void Case(string id, string mapping, object input, Action exercise)
{
    var digest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
        new { Version = fixtureVersion, Input = input }, new JsonSerializerOptions { IncludeFields = true }))).ToLowerInvariant();
    exercise();
    Console.WriteLine($"PASS {id}; fixture={fixtureVersion}; input-sha256={digest}; maps={mapping}");
}

void Require(bool condition, string name)
{
    if (!condition) throw new Exception($"Synthetic pilot composition failed: {name}");
    checks++;
}
