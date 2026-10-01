using AssessmentCoverage;
using AssessmentOrchestration;

internal static class SyntheticBaselineInventoryCases
{
    // Versioned value-free SYNTHETIC subset of TP-HAS-001/007/015/016; no live baseline eligibility claim.
    internal static void Run()
    {
        var passed = 0;
        var scope = new SyntheticAuthorizedScope("synthetic-customer", "synthetic-project", "synthetic-environment");
        var modules = new[] { new ModuleVersion("QBM", "synthetic-build-a"), new ModuleVersion("CCC", "synthetic-build-b") };
        var capability = new CapabilitySnapshot("synthetic-matrix-v1", CapabilityLifecycleState.FixtureVerified,
            "synthetic-product-build", "synthetic-schema-build", "synthetic-hotfix-digest", "synthetic-sql-build", 160,
            modules, "synthetic-pack-v1", "synthetic-normalization-v1", "synthetic-rules-v1");
        var compatibility = new BaselineCompatibility(capability.ProductBuild, capability.DatabaseSchemaBuild,
            capability.HotfixSetDigest, capability.SqlServerBuild, capability.CompatibilityLevel,
            modules.Reverse().ToArray(), capability.QueryPackVersion, capability.NormalizationSchemaVersion);
        var generic = Category("generic", SyntheticApplicability.RequiresAssessment);
        var semantic = Category("semantic", SyntheticApplicability.Unsupported, "synthetic-semantic-gap", "synthetic-planner");
        var deferred = Category("deferred", SyntheticApplicability.NotAssessed, "synthetic-deferred", "synthetic-planner");
        var absent = Category("absent", SyntheticApplicability.NotApplicable, "synthetic-uninstalled-feature", "synthetic-planner");
        var objects = new[]
        {
            Object("object-b", "CCC", "native-b", [semantic, generic, generic]),
            Object("object-a", "QBM", "native-a", [deferred, absent, generic])
        };
        var fixture = new SyntheticBaselineInventory(SyntheticBaselineInventoryPlanner.FixtureVersion,
            "synthetic-baseline-v1", scope, SyntheticBaselinePermission.Eligible, compatibility, objects);
        var expectedKeys = new[]
        {
            new CoverageKey("object-a", "absent"),
            new CoverageKey("object-a", "deferred"),
            new CoverageKey("object-a", "generic"),
            new CoverageKey("object-b", "generic"),
            new CoverageKey("object-b", "semantic")
        };
        var expectedDeclarations = new[]
        {
            new CoverageItem(expectedKeys[0], CoverageState.NotApplicable, "synthetic-uninstalled-feature", "synthetic-planner"),
            new CoverageItem(expectedKeys[1], CoverageState.NotAssessed, "synthetic-deferred", "synthetic-planner"),
            new CoverageItem(expectedKeys[4], CoverageState.Unsupported, "synthetic-semantic-gap", "synthetic-planner")
        };

        Check("SYN-A2-001 deterministic explicit inventory plan", () =>
        {
            var plan = RequirePlan(Plan(fixture));
            Assert(plan.FixtureVersion == "synthetic-baseline-inventory-v1" && plan.BaselineId == "synthetic-baseline-v1");
            Assert(plan.Scope == scope && plan.Permission == SyntheticBaselinePermission.Eligible && !plan.HasPermissionWarning);
            Assert(plan.ExpectedKeys.SequenceEqual(expectedKeys));
            Assert(plan.DeclaredItems.SequenceEqual(expectedDeclarations));
            Assert(plan.Objects.Select(item => item.InventoryId).SequenceEqual(new[] { "object-a", "object-b" }));
            Assert(plan.Objects[1].Categories.Count == 2);
            Assert(plan.CapabilityLock.StateAtLock == CapabilityLifecycleState.FixtureVerified);
            Assert(plan.CapabilityLock.LockDigest == CapabilityStartGuard.TryLock(capability, compatibility).Lock!.LockDigest);
            Assert(plan.DeclaredItems.All(item => item.State is not (CoverageState.Pass or CoverageState.Finding)));
        });

        Check("SYN-A2-002 input reordering and equivalent duplicate metadata", () =>
        {
            var reordered = fixture with
            {
                Compatibility = compatibility with { Modules = compatibility.Modules.Reverse().ToArray() },
                Objects = objects.Reverse().Select(item => item with { Categories = item.Categories.Reverse().ToArray() }).ToArray()
            };
            var plan = RequirePlan(Plan(reordered, capability with { Modules = modules.Reverse().ToArray() }));
            Assert(plan.ExpectedKeys.SequenceEqual(expectedKeys) && plan.DeclaredItems.SequenceEqual(expectedDeclarations));
            Assert(plan.CapabilityLock.LockDigest == RequirePlan(Plan(fixture)).CapabilityLock.LockDigest);
            Assert(plan.Objects.SelectMany(item => item.Categories).Select(item => item.CategoryId)
                .SequenceEqual(new[] { "absent", "deferred", "generic", "generic", "semantic" }));
        });

        Check("SYN-A2-003 permission warning remains visible without audit claim", () =>
        {
            var plan = RequirePlan(Plan(fixture with { Permission = SyntheticBaselinePermission.EligibleWithWarning }));
            Assert(plan.HasPermissionWarning && plan.Permission == SyntheticBaselinePermission.EligibleWithWarning);
        });

        Check("SYN-A2-004 absent native identities remain distinct", () =>
        {
            var plan = RequirePlan(Plan(fixture with
            {
                Objects = [Object("no-native-a", "QBM", null, [generic]), Object("no-native-b", "QBM", null, [generic])]
            }));
            Assert(plan.ExpectedKeys.SequenceEqual(new[]
            {
                new CoverageKey("no-native-a", "generic"), new CoverageKey("no-native-b", "generic")
            }));
        });

        Check("SYN-A2-005 native identity retains module and native type", () =>
        {
            var plan = RequirePlan(Plan(fixture with
            {
                Objects =
                [
                    Object("type-a", "QBM", "same-native", [generic]),
                    Object("type-b", "QBM", "same-native", [generic]) with { NativeType = "synthetic-other-type" },
                    Object("type-c", "CCC", "same-native", [generic])
                ]
            }));
            Assert(plan.Objects.Count == 3 && plan.ExpectedKeys.Count == 3);
        });

        Check("SYN-A2-006 declared gaps and pending work stay separate", () =>
        {
            var plan = RequirePlan(Plan(fixture));
            var progress = CoverageProgressProjector.Project(plan.ExpectedKeys, plan.DeclaredItems).Progress!;
            Assert(progress.PlannedUnits == 5 && progress.TerminalUnits == 3 && progress.RemainingUnits == 2 && !progress.AllTerminal);
            var terminal = CoverageCompletionProjector.Project(plan.ExpectedKeys, plan.DeclaredItems);
            Assert(!terminal.HasProjection && terminal.Issues.Count(item => item.Code == CoverageIssueCode.MissingResult) == 2);
            var completed = plan.DeclaredItems.Concat(new[]
            {
                new CoverageItem(expectedKeys[2], CoverageState.Pass), new CoverageItem(expectedKeys[3], CoverageState.Finding)
            }).ToArray();
            Assert(CoverageCompletionProjector.Project(plan.ExpectedKeys, completed).Kind == CoverageCompletionKind.CompleteWithGaps);
            Assert(ExecutableCoverageProjector.Project(plan.ExpectedKeys, completed).Measure == new ExecutableCoverageMeasure(2, 4));
        });

        Check("SYN-A2-007 zero applicable is unavailable", () =>
        {
            var plan = RequirePlan(Plan(fixture with { Objects = [Object("only", "QBM", null, [absent])] }));
            var executable = ExecutableCoverageProjector.Project(plan.ExpectedKeys, plan.DeclaredItems).Measure!;
            Assert(executable == new ExecutableCoverageMeasure(0, 0) && !executable.HasApplicableUnits);
        });

        Denial("SYN-A2-008 null fixture", SyntheticInventoryIssue.InvalidInput, null);
        Denial("SYN-A2-009 missing fixture version", SyntheticInventoryIssue.InvalidInput, fixture with { FixtureVersion = " " });
        Denial("SYN-A2-010 unknown fixture version", SyntheticInventoryIssue.UnknownFixtureVersion, fixture with { FixtureVersion = "synthetic-baseline-inventory-v2" });
        Denial("SYN-A2-011 missing baseline identity", SyntheticInventoryIssue.InvalidInput, fixture with { BaselineId = " " });
        Denial("SYN-A2-012 null scope", SyntheticInventoryIssue.InvalidInput, fixture with { Scope = null! });
        Denial("SYN-A2-013 incomplete scope", SyntheticInventoryIssue.InvalidInput, fixture with { Scope = scope with { CustomerId = " " } });
        Check("SYN-A2-014 missing authorized scope", () =>
            RequireIssue(SyntheticBaselineInventoryPlanner.Plan(capability, fixture, null), SyntheticInventoryIssue.InvalidInput));
        Check("SYN-A2-015 incomplete authorized scope", () =>
            RequireIssue(SyntheticBaselineInventoryPlanner.Plan(capability, fixture, scope with { EnvironmentId = " " }), SyntheticInventoryIssue.InvalidInput));
        Denial("SYN-A2-016 wrong customer", SyntheticInventoryIssue.WrongScope, fixture with { Scope = scope with { CustomerId = "other" } });
        Denial("SYN-A2-017 wrong project", SyntheticInventoryIssue.WrongScope, fixture with { Scope = scope with { ProjectId = "other" } });
        Denial("SYN-A2-018 wrong environment", SyntheticInventoryIssue.WrongScope, fixture with { Scope = scope with { EnvironmentId = "other" } });
        Denial("SYN-A2-019 ordinal scope", SyntheticInventoryIssue.WrongScope, fixture with { Scope = scope with { CustomerId = "SYNTHETIC-CUSTOMER" } });
        Denial("SYN-A2-020 blocked permission", SyntheticInventoryIssue.PermissionBlocked, fixture with { Permission = SyntheticBaselinePermission.Blocked });
        Denial("SYN-A2-021 unknown permission", SyntheticInventoryIssue.InvalidInput, fixture with { Permission = (SyntheticBaselinePermission)999 });
        Denial("SYN-A2-022 null objects", SyntheticInventoryIssue.InvalidInput, fixture with { Objects = null! });
        Denial("SYN-A2-023 empty inventory", SyntheticInventoryIssue.EmptyInventory, fixture with { Objects = [] });
        Denial("SYN-A2-024 null object", SyntheticInventoryIssue.InvalidObject, fixture with { Objects = [null!] });
        Denial("SYN-A2-025 missing object identity", SyntheticInventoryIssue.InvalidObject, fixture with { Objects = [objects[0] with { InventoryId = " " }] });
        Denial("SYN-A2-026 missing native type", SyntheticInventoryIssue.InvalidObject, fixture with { Objects = [objects[0] with { NativeType = " " }] });
        Denial("SYN-A2-027 malformed supplied native identity", SyntheticInventoryIssue.InvalidObject, fixture with { Objects = [objects[0] with { NativeIdentity = " " }] });
        Denial("SYN-A2-028 duplicate object identity", SyntheticInventoryIssue.DuplicateObjectId, fixture with { Objects = [objects[0], objects[0]] });
        Denial("SYN-A2-029 duplicate native identity is visible refusal", SyntheticInventoryIssue.DuplicateNativeIdentity,
            fixture with { Objects = [objects[0], objects[0] with { InventoryId = "distinct-baseline-object" }] });
        Denial("SYN-A2-030 unknown installed module", SyntheticInventoryIssue.InvalidModule, fixture with { Objects = [objects[0] with { ModuleId = "unknown" }] });
        Denial("SYN-A2-031 missing module", SyntheticInventoryIssue.InvalidModule, fixture with { Objects = [objects[0] with { ModuleId = " " }] });
        Denial("SYN-A2-032 null categories", SyntheticInventoryIssue.InvalidCategory, fixture with { Objects = [objects[0] with { Categories = null! }] });
        Denial("SYN-A2-033 empty categories cannot erase object", SyntheticInventoryIssue.InvalidCategory, fixture with { Objects = [objects[0] with { Categories = [] }] });
        Denial("SYN-A2-034 null category", SyntheticInventoryIssue.InvalidCategory, fixture with { Objects = [objects[0] with { Categories = [null!] }] });
        CategoryDenial("SYN-A2-035 missing category", generic with { CategoryId = " " });
        CategoryDenial("SYN-A2-036 unknown applicability", generic with { Applicability = (SyntheticApplicability)999 });
        CategoryDenial("SYN-A2-037 unexplained unsupported", semantic with { ReasonCode = null });
        CategoryDenial("SYN-A2-038 malformed unsupported reason", semantic with { ReasonCode = " " });
        CategoryDenial("SYN-A2-039 missing responsible stage", deferred with { ResponsibleStage = null });
        CategoryDenial("SYN-A2-040 malformed stage", absent with { ResponsibleStage = " " });
        CategoryDenial("SYN-A2-041 pending work cannot carry result metadata", generic with { ReasonCode = "pretend-result" });
        CategoryDenial("SYN-A2-042 pending work cannot carry a result stage", generic with { ResponsibleStage = "pretend-stage" });
        Denial("SYN-A2-043 conflicting category intent", SyntheticInventoryIssue.ConflictingCategoryMetadata,
            fixture with { Objects = [objects[0] with { Categories = [generic, semantic with { CategoryId = "generic" }] }] });
        Denial("SYN-A2-044 conflicting duplicate reason", SyntheticInventoryIssue.ConflictingCategoryMetadata,
            fixture with { Objects = [objects[0] with { Categories = [semantic, semantic with { ReasonCode = "different-gap" }] }] });
        Denial("SYN-A2-045 conflicting duplicate stage", SyntheticInventoryIssue.ConflictingCategoryMetadata,
            fixture with { Objects = [objects[0] with { Categories = [semantic, semantic with { ResponsibleStage = "different-stage" }] }] });

        CapabilityDenial("SYN-A2-046 missing compatibility", CapabilityLockIssue.InvalidInput, capability, fixture with { Compatibility = null! });
        CapabilityDenial("SYN-A2-047 missing capability", CapabilityLockIssue.InvalidInput, null, fixture);
        CapabilityDenial("SYN-A2-048 suspended capability", CapabilityLockIssue.Suspended, capability with { State = CapabilityLifecycleState.Suspended }, fixture);
        CapabilityDenial("SYN-A2-049 unsupported capability", CapabilityLockIssue.Unsupported, capability with { State = CapabilityLifecycleState.Unsupported }, fixture);
        CapabilityDenial("SYN-A2-050 product mismatch", CapabilityLockIssue.ExactVersionMismatch, capability, fixture with { Compatibility = compatibility with { ProductBuild = "changed" } });
        CapabilityDenial("SYN-A2-051 schema mismatch", CapabilityLockIssue.ExactVersionMismatch, capability, fixture with { Compatibility = compatibility with { DatabaseSchemaBuild = "changed" } });
        CapabilityDenial("SYN-A2-052 hotfix mismatch", CapabilityLockIssue.ExactVersionMismatch, capability, fixture with { Compatibility = compatibility with { HotfixSetDigest = "changed" } });
        CapabilityDenial("SYN-A2-053 SQL mismatch", CapabilityLockIssue.ExactVersionMismatch, capability, fixture with { Compatibility = compatibility with { SqlServerBuild = "changed" } });
        CapabilityDenial("SYN-A2-054 compatibility level mismatch", CapabilityLockIssue.ExactVersionMismatch, capability, fixture with { Compatibility = compatibility with { CompatibilityLevel = 150 } });
        CapabilityDenial("SYN-A2-055 query pack mismatch", CapabilityLockIssue.ExactVersionMismatch, capability, fixture with { Compatibility = compatibility with { QueryPackVersion = "changed" } });
        CapabilityDenial("SYN-A2-056 normalization mismatch", CapabilityLockIssue.ExactVersionMismatch, capability, fixture with { Compatibility = compatibility with { NormalizationSchemaVersion = "changed" } });
        CapabilityDenial("SYN-A2-057 module mismatch", CapabilityLockIssue.ModuleInventoryMismatch, capability, fixture with { Compatibility = compatibility with { Modules = [modules[0]] } });
        CapabilityDenial("SYN-A2-058 malformed module inventory", CapabilityLockIssue.InvalidInput, capability, fixture with { Compatibility = compatibility with { Modules = [modules[0], modules[0]] } });
        CapabilityDenial("SYN-A2-059 malformed capability module", CapabilityLockIssue.InvalidInput, capability with { Modules = [new ModuleVersion(" ", "build")] }, fixture);
        CapabilityDenial("SYN-A2-060 capability locking precedes inventory projection", CapabilityLockIssue.Suspended,
            capability with { State = CapabilityLifecycleState.Suspended }, fixture with { Objects = [] });

        Check("SYN-A2-061 snapshot and nested output immutability", () =>
        {
            var mutableModules = modules.ToArray();
            var mutableCategories = new[] { generic, semantic };
            var mutableObjects = new[] { Object("frozen", "CCC", "frozen-native", mutableCategories) };
            var input = fixture with { Objects = mutableObjects, Compatibility = compatibility with { Modules = mutableModules } };
            var plan = RequirePlan(Plan(input, capability with { Modules = mutableModules }));
            var digest = plan.CapabilityLock.LockDigest;
            mutableModules[0] = new ModuleVersion("changed", "changed");
            mutableCategories[0] = Category("changed", SyntheticApplicability.RequiresAssessment);
            mutableCategories[1] = semantic with { ReasonCode = "changed" };
            mutableObjects[0] = Object("changed", "QBM", null, []);
            Assert(plan.Objects.Single().InventoryId == "frozen");
            Assert(plan.ExpectedKeys.SequenceEqual(new[] { new CoverageKey("frozen", "generic"), new CoverageKey("frozen", "semantic") }));
            Assert(plan.DeclaredItems.Single() == new CoverageItem(new CoverageKey("frozen", "semantic"),
                CoverageState.Unsupported, "synthetic-semantic-gap", "synthetic-planner"));
            Assert(plan.Objects.Single().Categories.First().CategoryId == "generic");
            Assert(plan.CapabilityLock.LockDigest == digest && plan.CapabilityLock.Modules[0] == modules[1]);
            RequireReadOnly((IList<SyntheticInventoryObject>)plan.Objects);
            RequireReadOnly((IList<SyntheticCategoryDescriptor>)plan.Objects.Single().Categories);
            RequireReadOnly((IList<CoverageKey>)plan.ExpectedKeys);
            RequireReadOnly((IList<CoverageItem>)plan.DeclaredItems);
            RequireReadOnly((IList<ModuleVersion>)plan.CapabilityLock.Modules);
        });

        Check("SYN-A2-062 100000 immutable deterministic planned keys", () =>
        {
            var largeObjects = Enumerable.Range(0, 100_000).Reverse()
                .Select(index => Object($"synthetic-{index:D6}", "QBM", null, [generic])).ToArray();
            var plan = RequirePlan(Plan(fixture with { Objects = largeObjects }));
            Assert(plan.Objects.Count == 100_000 && plan.ExpectedKeys.Count == 100_000 && plan.DeclaredItems.Count == 0);
            Assert(plan.ExpectedKeys.Distinct().Count() == 100_000);
            Assert(plan.ExpectedKeys[0] == new CoverageKey("synthetic-000000", "generic"));
            Assert(plan.ExpectedKeys[^1] == new CoverageKey("synthetic-099999", "generic"));
            var progress = CoverageProgressProjector.Project(plan.ExpectedKeys, plan.DeclaredItems).Progress!;
            Assert(progress.PlannedUnits == 100_000 && progress.RemainingUnits == 100_000 && !progress.AllTerminal);
            largeObjects[0] = Object("changed", "QBM", null, [absent]);
            Assert(plan.Objects[^1].InventoryId == "synthetic-099999" && plan.DeclaredItems.Count == 0);
        });

        Console.WriteLine($"{passed} synthetic baseline inventory cases passed (TP-HAS-001/007/015/016 local subset).");

        SyntheticInventoryPlanResult Plan(SyntheticBaselineInventory? input, CapabilitySnapshot? snapshot = null) =>
            SyntheticBaselineInventoryPlanner.Plan(snapshot ?? capability, input, scope);

        void Denial(string id, SyntheticInventoryIssue issue, SyntheticBaselineInventory? input) =>
            Check(id, () => RequireIssue(Plan(input), issue));

        void CategoryDenial(string id, SyntheticCategoryDescriptor category) => Denial(id,
            SyntheticInventoryIssue.InvalidCategory, fixture with { Objects = [objects[0] with { Categories = [category] }] });

        void CapabilityDenial(string id, CapabilityLockIssue issue, CapabilitySnapshot? snapshot, SyntheticBaselineInventory input) =>
            Check(id, () =>
            {
                var result = SyntheticBaselineInventoryPlanner.Plan(snapshot, input, scope);
                Assert(!result.HasPlan && result.Plan is null && result.CapabilityIssue == issue && result.InventoryIssue is null);
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

    private static SyntheticCategoryDescriptor Category(string id, SyntheticApplicability applicability,
        string? reason = null, string? stage = null) => new(id, applicability, reason, stage);

    private static SyntheticInventoryObject Object(string id, string module, string? nativeIdentity,
        IReadOnlyCollection<SyntheticCategoryDescriptor> categories) => new(id, "synthetic-native-type", nativeIdentity, module, categories);

    private static SyntheticInventoryPlan RequirePlan(SyntheticInventoryPlanResult result)
    {
        Assert(result.HasPlan && result.InventoryIssue is null && result.CapabilityIssue is null);
        return result.Plan!;
    }

    private static void RequireIssue(SyntheticInventoryPlanResult result, SyntheticInventoryIssue issue) =>
        Assert(!result.HasPlan && result.Plan is null && result.InventoryIssue == issue && result.CapabilityIssue is null);

    private static void RequireReadOnly<T>(IList<T> list)
    {
        Assert(list.IsReadOnly);
        try
        {
            list.Clear();
        }
        catch (NotSupportedException)
        {
            return;
        }
        throw new Exception("Plan output was mutable.");
    }

    private static void Assert(bool condition)
    {
        if (!condition) throw new Exception("Unexpected synthetic inventory result.");
    }
}
