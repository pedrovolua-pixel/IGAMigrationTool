using AssessmentCoverage;
using AssessmentOrchestration;

internal static class CapabilityBoundCoverageCases
{
    // Synthetic local composition subset of TP-HAS-001 and TP-HAS-007.
    internal static void Run(CapabilitySnapshot capability, BaselineCompatibility baseline)
    {
        var passed = 0;
        var keys = new[] { Key("one"), Key("two"), Key("three") };
        var clean = new[]
        {
            new CoverageItem(keys[0], CoverageState.Pass),
            new CoverageItem(keys[1], CoverageState.Finding),
            new CoverageItem(keys[2], CoverageState.NotApplicable, "not-installed", "planner")
        };

        Check("SYN-A1-001 clean terminal composition", () =>
        {
            var result = Project(keys, clean);
            var projection = RequireProjection(result);
            Assert(projection.CapabilityLock.LockDigest == CapabilityStartGuard.TryLock(capability, baseline).Lock!.LockDigest);
            Assert(projection.CompletionKind == CoverageCompletionKind.Complete);
            Assert(projection.Counts.Single(count => count.State == CoverageState.Pass).Count == 1);
            Assert(projection.Counts.Single(count => count.State == CoverageState.Finding).Count == 1);
            Assert(projection.Counts.Single(count => count.State == CoverageState.NotApplicable).Count == 1);
            Assert(projection.Counts.Sum(count => count.Count) == keys.Length);
            Assert(projection.ExecutableCoverage == new ExecutableCoverageMeasure(2, 2));
            Assert(projection.Limitations.Count == 0);
        });

        Check("SYN-A1-002 all explicit limitations", () =>
        {
            var gapStates = Enum.GetValues<CoverageState>()
                .Where(state => state is not (CoverageState.Pass or CoverageState.Finding or CoverageState.NotApplicable))
                .ToArray();
            var gapKeys = gapStates.Select(state => Key(state.ToString())).ToArray();
            var gapItems = gapStates.Select((state, index) =>
                new CoverageItem(gapKeys[index], state, "synthetic-reason", "synthetic-stage")).ToArray();
            var projection = RequireProjection(Project(gapKeys, gapItems));
            Assert(projection.CompletionKind == CoverageCompletionKind.CompleteWithGaps);
            Assert(projection.Limitations.Count == gapStates.Length);
            Assert(projection.Limitations.Sum(item => item.Count) == gapStates.Length);
            Assert(projection.ExecutableCoverage == new ExecutableCoverageMeasure(0, gapStates.Length));
        });

        Check("SYN-A1-003 grouped limitations retain successful units", () =>
        {
            var items = new[]
            {
                clean[0],
                new CoverageItem(keys[1], CoverageState.Error, "synthetic-error", "rule"),
                new CoverageItem(keys[2], CoverageState.Error, "synthetic-error", "rule")
            };
            var projection = RequireProjection(Project(keys, items));
            Assert(projection.CompletionKind == CoverageCompletionKind.CompleteWithGaps);
            Assert(projection.Limitations.Single() == new CoverageLimitation(CoverageState.Error, "synthetic-error", "rule", 2));
            Assert(projection.ExecutableCoverage == new ExecutableCoverageMeasure(1, 3));
        });

        Check("SYN-A1-004 zero applicable units", () =>
        {
            var items = keys.Select(key => new CoverageItem(key, CoverageState.NotApplicable, "not-installed", "planner")).ToArray();
            var projection = RequireProjection(Project(keys, items));
            Assert(projection.CompletionKind == CoverageCompletionKind.Complete);
            Assert(projection.ExecutableCoverage == new ExecutableCoverageMeasure(0, 0));
            Assert(!projection.ExecutableCoverage.HasApplicableUnits);
        });

        CheckCapability("SYN-A1-005 suspended precedence", CapabilityLockIssue.Suspended,
            capability with { State = CapabilityLifecycleState.Suspended }, baseline, null, null);
        CheckCapability("SYN-A1-006 unsupported", CapabilityLockIssue.Unsupported,
            capability with { State = CapabilityLifecycleState.Unsupported }, baseline, keys, clean);
        CheckCapability("SYN-A1-007 invalid descriptor precedence", CapabilityLockIssue.InvalidInput,
            null, baseline, null, null);
        CheckCapability("SYN-A1-008 schema mismatch", CapabilityLockIssue.ExactVersionMismatch,
            capability, baseline with { DatabaseSchemaBuild = "schema-v2" }, keys, clean);
        CheckCapability("SYN-A1-009 query mismatch", CapabilityLockIssue.ExactVersionMismatch,
            capability, baseline with { QueryPackVersion = "pack-v2" }, keys, clean);
        CheckCapability("SYN-A1-010 normalization mismatch", CapabilityLockIssue.ExactVersionMismatch,
            capability, baseline with { NormalizationSchemaVersion = "normalized-v2" }, keys, clean);
        CheckCapability("SYN-A1-011 module mismatch", CapabilityLockIssue.ModuleInventoryMismatch,
            capability, baseline with { Modules = [] }, keys, clean);

        CheckCoverage("SYN-A1-012 empty plan", CoverageIssueCode.InvalidInput, [], []);
        CheckCoverage("SYN-A1-013 null plan", CoverageIssueCode.InvalidInput, null, clean);
        CheckCoverage("SYN-A1-014 null results", CoverageIssueCode.InvalidInput, keys, null);
        CheckCoverage("SYN-A1-015 missing result", CoverageIssueCode.MissingResult, keys, clean[..2]);
        CheckCoverage("SYN-A1-016 duplicate result", CoverageIssueCode.DuplicateResult, keys, [.. clean, clean[0]]);
        CheckCoverage("SYN-A1-017 unexpected result", CoverageIssueCode.UnexpectedResult, keys,
            [.. clean, new CoverageItem(Key("unknown"), CoverageState.Pass)]);
        CheckCoverage("SYN-A1-018 duplicate expected key", CoverageIssueCode.DuplicateExpectedKey, [.. keys, keys[0]], clean);
        CheckCoverage("SYN-A1-019 invalid expected key", CoverageIssueCode.InvalidKey, [.. keys, Key(" ")], clean);
        CheckCoverage("SYN-A1-020 invalid state", CoverageIssueCode.InvalidState, keys,
            [clean[0], clean[1], new CoverageItem(keys[2], (CoverageState)999)]);
        CheckCoverage("SYN-A1-021 unexplained gap", CoverageIssueCode.MissingGapReason, keys,
            [clean[0], clean[1], new CoverageItem(keys[2], CoverageState.InsufficientEvidence, ResponsibleStage: "rule")]);
        CheckCoverage("SYN-A1-022 missing stage", CoverageIssueCode.MissingResponsibleStage, keys,
            [clean[0], clean[1], new CoverageItem(keys[2], CoverageState.Redacted, "redacted")]);

        Check("SYN-A1-023 snapshotted input and readonly output", () =>
        {
            var mutableModules = baseline.Modules.ToArray();
            var mutableKeys = keys.ToArray();
            var mutableItems = clean.ToArray();
            var projection = RequireProjection(CapabilityBoundCoverageProjector.Project(
                capability with { Modules = mutableModules }, baseline with { Modules = mutableModules }, mutableKeys, mutableItems));
            var digest = projection.CapabilityLock.LockDigest;
            var firstModule = projection.CapabilityLock.Modules[0];
            mutableModules[0] = new ModuleVersion("changed", "changed");
            mutableKeys[0] = Key("changed");
            mutableItems[0] = new CoverageItem(keys[0], CoverageState.Error, "changed", "changed");
            Assert(projection.CapabilityLock.LockDigest == digest && projection.CapabilityLock.Modules[0] == firstModule);
            Assert(projection.ExecutableCoverage == new ExecutableCoverageMeasure(2, 2));
            Assert(projection.Limitations.Count == 0);
            Assert(((IList<CoverageStateCount>)projection.Counts).IsReadOnly);
            Assert(((IList<CoverageLimitation>)projection.Limitations).IsReadOnly);
            Assert(((IList<ModuleVersion>)projection.CapabilityLock.Modules).IsReadOnly);
        });

        Check("SYN-A1-024 readonly coverage issues", () =>
        {
            var result = Project(keys, clean[..2]);
            Assert(!result.HasProjection && ((IList<CoverageIssue>)result.CoverageIssues).IsReadOnly);
        });

        Check("SYN-A1-025 100000 planned units", () =>
        {
            var scaleKeys = Enumerable.Range(0, 100_000).Select(index => Key($"synthetic-{index}")).ToArray();
            var items = scaleKeys.Select((key, index) => (index % 4) switch
            {
                0 => new CoverageItem(key, CoverageState.Pass),
                1 => new CoverageItem(key, CoverageState.Finding),
                2 => new CoverageItem(key, CoverageState.NotApplicable, "not-installed", "planner"),
                _ => new CoverageItem(key, CoverageState.Inaccessible, "synthetic-denial", "reader")
            }).ToArray();
            var projection = RequireProjection(Project(scaleKeys, items));
            Assert(projection.CompletionKind == CoverageCompletionKind.CompleteWithGaps);
            Assert(projection.Counts.Sum(count => count.Count) == 100_000);
            Assert(projection.ExecutableCoverage == new ExecutableCoverageMeasure(50_000, 75_000));
            Assert(projection.Limitations.Single().Count == 25_000);
        });

        Console.WriteLine($"{passed} capability-bound coverage composition cases passed (TP-HAS-001/007 local subset).");

        CapabilityBoundCoverageResult Project(IReadOnlyCollection<CoverageKey>? expected, IReadOnlyCollection<CoverageItem>? results) =>
            CapabilityBoundCoverageProjector.Project(capability, baseline, expected, results);

        void CheckCapability(string id, CapabilityLockIssue issue, CapabilitySnapshot? snapshot,
            BaselineCompatibility source, IReadOnlyCollection<CoverageKey>? expected, IReadOnlyCollection<CoverageItem>? results) =>
            Check(id, () =>
            {
                var result = CapabilityBoundCoverageProjector.Project(snapshot, source, expected, results);
                Assert(result.CapabilityIssue == issue && !result.HasProjection && result.CoverageIssues.Count == 0);
            });

        void CheckCoverage(string id, CoverageIssueCode issue, IReadOnlyCollection<CoverageKey>? expected,
            IReadOnlyCollection<CoverageItem>? results) => Check(id, () =>
            {
                var result = Project(expected, results);
                Assert(result.CapabilityIssue is null && !result.HasProjection && result.CoverageIssues.Any(item => item.Code == issue));
            });

        void Check(string id, Action action)
        {
            try
            {
                action();
                passed++;
            }
            catch (Exception exception)
            {
                throw new Exception($"Failed: {id}", exception);
            }
        }
    }

    private static CoverageKey Key(string id) => new(id, "synthetic-category");

    private static CapabilityBoundCoverageProjection RequireProjection(CapabilityBoundCoverageResult result)
    {
        Assert(result.HasProjection && result.CapabilityIssue is null && result.CoverageIssues.Count == 0);
        return result.Projection!;
    }

    private static void Assert(bool condition)
    {
        if (!condition) throw new Exception("Unexpected synthetic composition result.");
    }
}
