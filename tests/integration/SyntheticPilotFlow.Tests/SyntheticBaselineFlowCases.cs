using System.Security.Cryptography;
using System.Text.Json;
using AssessmentCoverage;
using AssessmentOrchestration;

internal static class SyntheticBaselineFlowCases
{
    private const string FixtureVersion = "synthetic-baseline-flow-v1";

    internal static void Run()
    {
        var assertions = 0;
        var fixtures = 0;
        var scope = new SyntheticAuthorizedScope("SYN-CUSTOMER", "SYN-PROJECT", "SYN-ENVIRONMENT");
        var modules = new[] { new ModuleVersion("QBM", "fixture-module-1"), new ModuleVersion("QER", "fixture-module-2") };
        var capability = new CapabilitySnapshot("fixture-matrix-1", CapabilityLifecycleState.FixtureVerified,
            "10.fixture.1", "fixture-schema-1", "fixture-hotfix-1", "fixture-sql-1", 160,
            modules, "fixture-pack-1", "fixture-normalization-1", "fixture-rules-1");
        var compatibility = new BaselineCompatibility("10.fixture.1", "fixture-schema-1", "fixture-hotfix-1",
            "fixture-sql-1", 160, modules, "fixture-pack-1", "fixture-normalization-1");
        var baseline = new SyntheticBaselineInventory("synthetic-baseline-inventory-v1", "SYN-BASELINE", scope,
            SyntheticBaselinePermission.Eligible, compatibility,
            [new("object-b", "BusinessRole", "native-b", "QER",
                [new("schema", SyntheticApplicability.RequiresAssessment),
                 new("support", SyntheticApplicability.Unsupported, "SEMANTIC-DEFERRED", "planner")]),
             new("object-a", "Process", null, "QBM",
                [new("unavailable", SyntheticApplicability.NotAssessed, "NOT-EXECUTED", "planner"),
                 new("schema", SyntheticApplicability.RequiresAssessment),
                 new("not-applicable", SyntheticApplicability.NotApplicable, "FEATURE-NOT-APPLICABLE", "planner")])]);
        var expectedKeys = new CoverageKey[]
        {
            new("object-a", "not-applicable"), new("object-a", "schema"), new("object-a", "unavailable"),
            new("object-b", "schema"), new("object-b", "support")
        };
        var expectedDeclared = new CoverageItem[]
        {
            new(expectedKeys[0], CoverageState.NotApplicable, "FEATURE-NOT-APPLICABLE", "planner"),
            new(expectedKeys[2], CoverageState.NotAssessed, "NOT-EXECUTED", "planner"),
            new(expectedKeys[4], CoverageState.Unsupported, "SEMANTIC-DEFERRED", "planner")
        };
        var expectedTerminal = new CoverageItem[]
        {
            expectedDeclared[0], new(expectedKeys[1], CoverageState.Pass), expectedDeclared[1],
            new(expectedKeys[3], CoverageState.Finding), expectedDeclared[2]
        };

        Case("SYN-BASELINE-001", baseline, () =>
        {
            var plan = RequirePlan(baseline);
            Require(plan.ExpectedKeys.SequenceEqual(expectedKeys), "five independently specified keys in ordinal order");
            Require(plan.DeclaredItems.SequenceEqual(expectedDeclared), "three explicit declared states and explanations");
            Require(plan.BaselineId == "SYN-BASELINE" && plan.Scope == scope &&
                plan.FixtureVersion == "synthetic-baseline-inventory-v1", "baseline identity and scope preserved");
            Require(plan.Permission == SyntheticBaselinePermission.Eligible && !plan.HasPermissionWarning,
                "minimum read descriptor remains eligible without warning");
            Require(plan.CapabilityLock.ProductBuild == "10.fixture.1" && plan.CapabilityLock.QueryPackVersion == "fixture-pack-1" &&
                plan.CapabilityLock.RuleCatalogVersion == "fixture-rules-1", "expected exact version tuple retained");
            Require(plan.Objects.Select(item => item.InventoryId).SequenceEqual(new[] { "object-a", "object-b" }),
                "objects sorted without erasing native types");
            Require(plan.Objects[0].NativeType == "Process" && plan.Objects[0].NativeIdentity is null &&
                plan.Objects[1].NativeType == "BusinessRole" && plan.Objects[1].NativeIdentity == "native-b",
                "optional native identity and distinct native types survive");
        });

        Case("SYN-BASELINE-002", new { baseline, Partial = expectedDeclared }, () =>
        {
            var plan = RequirePlan(baseline);
            var progress = CoverageProgressProjector.Project(plan.ExpectedKeys, plan.DeclaredItems);
            Require(progress.Progress is { PlannedUnits: 5, TerminalUnits: 3, RemainingUnits: 2, AllTerminal: false },
                "explicit gaps count as terminal but unexecuted required keys remain outstanding");
            var terminal = CapabilityBoundCoverageProjector.Project(capability, compatibility, plan.ExpectedKeys, plan.DeclaredItems);
            Require(!terminal.HasProjection && terminal.Projection is null &&
                terminal.CoverageIssues.Count(issue => issue.Code == CoverageIssueCode.MissingResult) == 2,
                "missing required results cannot falsely complete");
        });

        Case("SYN-BASELINE-003", new { baseline, Terminal = expectedTerminal }, () =>
        {
            var plan = RequirePlan(baseline);
            var terminal = CapabilityBoundCoverageProjector.Project(capability, compatibility, plan.ExpectedKeys, expectedTerminal);
            Require(terminal.HasProjection && terminal.CapabilityIssue is null && terminal.CoverageIssues.Count == 0,
                "all five independently supplied terminal results reconcile");
            var projection = terminal.Projection!;
            Require(projection.CompletionKind == CoverageCompletionKind.CompleteWithGaps &&
                projection.ExecutableCoverage == new ExecutableCoverageMeasure(2, 4), "two executed out of four applicable keys");
            var counts = new CoverageStateCount[]
            {
                new(CoverageState.Pass, 1), new(CoverageState.Finding, 1), new(CoverageState.NotApplicable, 1),
                new(CoverageState.NotAssessed, 1), new(CoverageState.InsufficientEvidence, 0), new(CoverageState.Excluded, 0),
                new(CoverageState.Inaccessible, 0), new(CoverageState.Redacted, 0), new(CoverageState.Unsupported, 1),
                new(CoverageState.Error, 0)
            };
            Require(projection.Counts.SequenceEqual(counts), "literal count for each of ten states");
            Require(projection.Limitations.SequenceEqual(new CoverageLimitation[]
            {
                new(CoverageState.NotAssessed, "NOT-EXECUTED", "planner", 1),
                new(CoverageState.Unsupported, "SEMANTIC-DEFERRED", "planner", 1)
            }), "two independently specified reason and stage limitation groups");
            Require(projection.CapabilityLock == plan.CapabilityLock ||
                projection.CapabilityLock.LockDigest == plan.CapabilityLock.LockDigest, "terminal composition retains planned tuple");
        });

        var wrongScopes = new[]
        {
            scope with { CustomerId = "OTHER-CUSTOMER" }, scope with { ProjectId = "OTHER-PROJECT" },
            scope with { EnvironmentId = "OTHER-ENVIRONMENT" }
        };
        var wrongScopeInputs = wrongScopes.Select(wrongScope => baseline with { Scope = wrongScope }).ToArray();
        Case("SYN-BASELINE-004", wrongScopeInputs, () =>
        {
            foreach (var input in wrongScopeInputs)
                Deny(input, SyntheticInventoryIssue.WrongScope);
        });

        var capabilityDenials = new CapabilityDenial[]
        {
            new(capability with { State = CapabilityLifecycleState.Suspended }, baseline, CapabilityLockIssue.Suspended),
            new(capability with { State = CapabilityLifecycleState.Unsupported }, baseline, CapabilityLockIssue.Unsupported),
            new(capability, baseline with { Compatibility = compatibility with { ProductBuild = "10.fixture.2" } }, CapabilityLockIssue.ExactVersionMismatch),
            new(capability, baseline with { Compatibility = compatibility with { DatabaseSchemaBuild = "fixture-schema-2" } }, CapabilityLockIssue.ExactVersionMismatch),
            new(capability, baseline with { Compatibility = compatibility with { HotfixSetDigest = "fixture-hotfix-2" } }, CapabilityLockIssue.ExactVersionMismatch),
            new(capability, baseline with { Compatibility = compatibility with { SqlServerBuild = "fixture-sql-2" } }, CapabilityLockIssue.ExactVersionMismatch),
            new(capability, baseline with { Compatibility = compatibility with { CompatibilityLevel = 150 } }, CapabilityLockIssue.ExactVersionMismatch),
            new(capability, baseline with { Compatibility = compatibility with { QueryPackVersion = "fixture-pack-2" } }, CapabilityLockIssue.ExactVersionMismatch),
            new(capability, baseline with { Compatibility = compatibility with { NormalizationSchemaVersion = "fixture-normalization-2" } }, CapabilityLockIssue.ExactVersionMismatch),
            new(capability, baseline with { Compatibility = compatibility with { Modules = [] } }, CapabilityLockIssue.ModuleInventoryMismatch)
        };
        Case("SYN-BASELINE-005", capabilityDenials, () =>
        {
            foreach (var denial in capabilityDenials) DenyCapability(denial.Capability, denial.Input, denial.Issue);
        });

        var warnedInput = baseline with { Permission = SyntheticBaselinePermission.EligibleWithWarning };
        var permissionDenials = new InventoryDenial[]
        {
            new(baseline with { Permission = SyntheticBaselinePermission.Blocked }, SyntheticInventoryIssue.PermissionBlocked),
            new(baseline with { Permission = (SyntheticBaselinePermission)999 }, SyntheticInventoryIssue.InvalidInput)
        };
        Case("SYN-BASELINE-006", new { warnedInput, permissionDenials }, () =>
        {
            var warned = RequirePlan(warnedInput);
            Require(warned.Permission == SyntheticBaselinePermission.EligibleWithWarning && warned.HasPermissionWarning,
                "excess read-only warning descriptor retained without claiming audit execution");
            foreach (var denial in permissionDenials) Deny(denial.Input, denial.Issue);
        });

        var headerDenials = new InventoryDenial[]
        {
            new(null, SyntheticInventoryIssue.InvalidInput),
            new(baseline with { FixtureVersion = "synthetic-baseline-inventory-v999" }, SyntheticInventoryIssue.UnknownFixtureVersion),
            new(baseline with { BaselineId = " " }, SyntheticInventoryIssue.InvalidInput),
            new(baseline with { Scope = null! }, SyntheticInventoryIssue.InvalidInput),
            new(baseline with { Objects = null! }, SyntheticInventoryIssue.InvalidInput)
        };
        var nullCompatibility = baseline with { Compatibility = null! };
        Case("SYN-BASELINE-007", new { headerDenials, nullCompatibility }, () =>
        {
            foreach (var denial in headerDenials) Deny(denial.Input, denial.Issue);
            DenyCapability(capability, nullCompatibility, CapabilityLockIssue.InvalidInput);
        });

        var originalObjects = baseline.Objects.ToArray();
        var objectDenials = new InventoryDenial[]
        {
            new(baseline with { Objects = [originalObjects[0], originalObjects[0]] }, SyntheticInventoryIssue.DuplicateObjectId),
            new(baseline with { Objects = [originalObjects[0], originalObjects[0] with { InventoryId = "different-id" }] }, SyntheticInventoryIssue.DuplicateNativeIdentity),
            new(baseline with { Objects = [originalObjects[0] with { InventoryId = " " }] }, SyntheticInventoryIssue.InvalidObject),
            new(baseline with { Objects = [originalObjects[0] with { NativeType = " " }] }, SyntheticInventoryIssue.InvalidObject),
            new(baseline with { Objects = [originalObjects[0] with { NativeIdentity = " " }] }, SyntheticInventoryIssue.InvalidObject),
            new(baseline with { Objects = [originalObjects[0] with { ModuleId = "UNINSTALLED" }] }, SyntheticInventoryIssue.InvalidModule),
            new(baseline with { Objects = [originalObjects[0] with { Categories = [] }] }, SyntheticInventoryIssue.InvalidCategory),
            new(baseline with { Objects = [originalObjects[0] with { Categories = [new(" ", SyntheticApplicability.RequiresAssessment)] }] }, SyntheticInventoryIssue.InvalidCategory),
            new(baseline with { Objects = [originalObjects[0] with { Categories = [new("schema", (SyntheticApplicability)999)] }] }, SyntheticInventoryIssue.InvalidCategory),
            new(baseline with { Objects = [originalObjects[0] with { Categories = [new("schema", SyntheticApplicability.Unsupported)] }] }, SyntheticInventoryIssue.InvalidCategory),
            new(baseline with { Objects = [originalObjects[0] with { Categories = [new("schema", SyntheticApplicability.RequiresAssessment, "UNEXPECTED", "planner")] }] }, SyntheticInventoryIssue.InvalidCategory),
            new(baseline with { Objects = [originalObjects[0] with { Categories =
                [new("schema", SyntheticApplicability.RequiresAssessment), new("schema", SyntheticApplicability.Unsupported, "DEFERRED", "planner")] }] },
                SyntheticInventoryIssue.ConflictingCategoryMetadata)
        };
        Case("SYN-BASELINE-008", objectDenials, () =>
        {
            foreach (var denial in objectDenials) Deny(denial.Input, denial.Issue);
        });

        Case("SYN-BASELINE-009", baseline with { Objects = [] }, () =>
        {
            Deny(baseline with { Objects = [] }, SyntheticInventoryIssue.EmptyInventory);
            Require(!CoverageCompletionProjector.Project([], []).HasProjection, "empty inventory never means perfect completion");
        });

        var categories = originalObjects[0].Categories.Reverse().ToArray();
        var objects = new[] { originalObjects[1], originalObjects[0] with { Categories = categories } };
        var reorderedInput = baseline with { Objects = objects };
        var changedCategory = new SyntheticCategoryDescriptor("changed", SyntheticApplicability.RequiresAssessment);
        var changedObject = objects[0] with { InventoryId = "changed" };
        var duplicate = originalObjects[0].Categories.First();
        var duplicateCategoryInput = baseline with
        {
            Objects =
            [originalObjects[0] with { Categories = [.. originalObjects[0].Categories, duplicate] }, originalObjects[1]]
        };
        Case("SYN-BASELINE-010", new { reorderedInput, changedCategory, changedObject, duplicateCategoryInput }, () =>
        {
            var reordered = RequirePlan(reorderedInput);
            Require(reordered.ExpectedKeys.SequenceEqual(expectedKeys) && reordered.DeclaredItems.SequenceEqual(expectedDeclared),
                "reordering cannot change keys or explicit gaps");
            categories[0] = changedCategory;
            objects[0] = changedObject;
            Require(reordered.ExpectedKeys.SequenceEqual(expectedKeys) && reordered.Objects[0].InventoryId == "object-a" &&
                reordered.Objects[1].Categories.All(category => category.CategoryId != "changed"), "caller mutation cannot alter frozen plan");
            Require(((IList<CoverageKey>)reordered.ExpectedKeys).IsReadOnly && ((IList<CoverageItem>)reordered.DeclaredItems).IsReadOnly &&
                ((IList<SyntheticInventoryObject>)reordered.Objects).IsReadOnly &&
                ((IList<SyntheticCategoryDescriptor>)reordered.Objects[1].Categories).IsReadOnly, "nested plan collections are read-only");
            var deduplicated = RequirePlan(duplicateCategoryInput);
            Require(deduplicated.ExpectedKeys.SequenceEqual(expectedKeys) && deduplicated.Objects[1].Categories.Count == 2,
                "exact repeated category metadata is idempotent");
        });

        const int scale = 100_000;
        var scaleObjects = Enumerable.Range(0, scale).Select(index => new SyntheticInventoryObject(
            $"scale-{index:D6}", "Process", null, "QBM", [new("schema", SyntheticApplicability.RequiresAssessment)])).ToArray();
        var scaleInput = baseline with { Objects = scaleObjects };
        var scaleTerminal = Enumerable.Range(0, scale).Select(index => new CoverageItem(
            new CoverageKey($"scale-{index:D6}", "schema"), CoverageState.Pass)).ToArray();
        Case("SYN-BASELINE-011", new { Baseline = scaleInput, Terminal = scaleTerminal, Generator = "ascending-zero-padded-object-v1", Count = scale }, () =>
        {
            var plan = RequirePlan(scaleInput);
            Require(plan.ExpectedKeys.Count == 100_000 && plan.DeclaredItems.Count == 0 &&
                plan.ExpectedKeys[0] == new CoverageKey("scale-000000", "schema") &&
                plan.ExpectedKeys[^1] == new CoverageKey("scale-099999", "schema"), "independent scale count and boundary keys");
            var partial = scaleTerminal[..^1];
            Require(CoverageProgressProjector.Project(plan.ExpectedKeys, partial).Progress is
            { PlannedUnits: 100_000, TerminalUnits: 99_999, RemainingUnits: 1, AllTerminal: false }, "one missing scale result remains outstanding");
            var missing = CapabilityBoundCoverageProjector.Project(capability, compatibility, plan.ExpectedKeys, partial);
            Require(!missing.HasProjection && missing.CoverageIssues.Single() ==
                new CoverageIssue(CoverageIssueCode.MissingResult, new CoverageKey("scale-099999", "schema")), "missing scale key has exact typed denial");
            var complete = CapabilityBoundCoverageProjector.Project(capability, compatibility, plan.ExpectedKeys, scaleTerminal).Projection;
            Require(complete is not null && complete.CompletionKind == CoverageCompletionKind.Complete &&
                complete.ExecutableCoverage == new ExecutableCoverageMeasure(100_000, 100_000) && complete.Limitations.Count == 0 &&
                complete.Counts.Single(count => count.State == CoverageState.Pass).Count == 100_000 &&
                complete.Counts.Where(count => count.State != CoverageState.Pass).All(count => count.Count == 0), "complete scale plan has independently expected counts");
        });

        Console.WriteLine($"{assertions} synthetic baseline-to-coverage assertions passed across {fixtures} versioned fixtures.");
        Console.WriteLine("Scope: internal synthetic inventory only; trusted baseline adapter, authorization, applicability inference and durable run NOT VERIFIED.");

        SyntheticInventoryPlan RequirePlan(SyntheticBaselineInventory input)
        {
            var result = SyntheticBaselineInventoryPlanner.Plan(capability, input, scope);
            Require(result.HasPlan && result.Plan is not null && result.InventoryIssue is null && result.CapabilityIssue is null,
                "synthetic inventory plan available");
            return result.Plan!;
        }

        void Deny(SyntheticBaselineInventory? input, SyntheticInventoryIssue expected)
        {
            var result = SyntheticBaselineInventoryPlanner.Plan(capability, input, scope);
            Require(!result.HasPlan && result.Plan is null && result.InventoryIssue == expected && result.CapabilityIssue is null,
                "typed inventory denial exposes no plan");
        }

        void DenyCapability(CapabilitySnapshot candidate, SyntheticBaselineInventory input, CapabilityLockIssue expected)
        {
            var result = SyntheticBaselineInventoryPlanner.Plan(candidate, input, scope);
            Require(!result.HasPlan && result.Plan is null && result.InventoryIssue is null && result.CapabilityIssue == expected,
                "typed capability denial exposes no plan");
        }

        void Case(string id, object input, Action exercise)
        {
            var digest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
                new { Version = FixtureVersion, Capability = capability, AuthorizedScope = scope, Input = input }))).ToLowerInvariant();
            exercise();
            fixtures++;
            Console.WriteLine($"PASS {id}; fixture={FixtureVersion}; input-sha256={digest}; maps=FR-HAS-2/5/29;TP-HAS-001/007/015/016-local-subset");
        }

        void Require(bool condition, string name)
        {
            if (!condition) throw new Exception($"Synthetic baseline flow failed: {name}");
            assertions++;
        }
    }

    private sealed record InventoryDenial(SyntheticBaselineInventory? Input, SyntheticInventoryIssue Issue);
    private sealed record CapabilityDenial(CapabilitySnapshot Capability, SyntheticBaselineInventory Input, CapabilityLockIssue Issue);
}
