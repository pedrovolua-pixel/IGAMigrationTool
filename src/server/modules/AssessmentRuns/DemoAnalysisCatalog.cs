using AssessmentCoverage;
using AssessmentOrchestration;
using DeterministicAnalysis;

namespace AssessmentRuns;

/// <summary>Opt-in new catalog presets; historical coverage-only fixtures remain intact.</summary>
public static class DemoAnalysisCatalog
{
    public static bool IsAnalysisBaseline(string id) => SyntheticAnalysisFixturePack.Presets.Any(item => item.Id == id);
    public static bool IsAnalysisProfile(string id) => SyntheticAnalysisFixturePack.Profiles.Any(item => item.Id == id);
    public static bool Compatible(string baselineId, string profileId) => IsAnalysisBaseline(baselineId) == IsAnalysisProfile(profileId);
    public static SyntheticAnalysisLock Freeze(string baselineId, string profileId) =>
        SyntheticAnalysisFixturePack.Freeze(SyntheticAnalysisFixturePack.Scope, baselineId, profileId);
    public static string FrozenDigest(string baselineId, string profileId) => SyntheticCanonicalDigest.Compute(Freeze(baselineId, profileId));
    public static IEnumerable<SyntheticDemoProfile> CreateProfiles() => SyntheticAnalysisFixturePack.Profiles.Select(profile => new SyntheticDemoProfile(
        profile.Id, profile.Label, new(profile.Version, null, profile.AlgorithmVersion, "synthetic-ai-disabled-v1",
            "synthetic-prompt-disabled-v1", "synthetic-model-disabled-v1", "synthetic-analysis-app-v1",
            SyntheticDurableRunEngine.WorkSchemaVersion, new string('0', 64))));
    public static IEnumerable<SyntheticDemoBaseline> CreateBaselines()
    {
        var modules = SyntheticAnalysisFixturePack.Rules.Select(rule => rule.ModuleId).Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).Select(id => new ModuleVersion(id, "synthetic-module-v1")).ToArray();
        var capability = new CapabilitySnapshot("synthetic-analysis-matrix-v1", CapabilityLifecycleState.FixtureVerified,
            SyntheticAnalysisFixturePack.Compatibility.ProductVersion, SyntheticAnalysisFixturePack.Compatibility.EvidenceSchemaVersion,
            "synthetic-hotfix-digest", "synthetic-sql-build", 160, Array.AsReadOnly(modules),
            "synthetic-query-pack-v1", "synthetic-normalization-v1", SyntheticAnalysisFixturePack.CatalogVersion);
        var compatibility = new BaselineCompatibility(capability.ProductBuild, capability.DatabaseSchemaBuild,
            capability.HotfixSetDigest, capability.SqlServerBuild, capability.CompatibilityLevel, capability.Modules,
            capability.QueryPackVersion, capability.NormalizationSchemaVersion);
        foreach (var preset in SyntheticAnalysisFixturePack.Presets)
        {
            var planned = SyntheticAnalysisEngine.Plan(Freeze(preset.Id, SyntheticAnalysisFixturePack.Profiles[0].Id));
            var objects = planned.Units.GroupBy(unit => unit.ObjectId).OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new SyntheticInventoryObject(group.Key, group.First().ObjectType, null, group.First().ModuleId,
                    Array.AsReadOnly(group.Select(unit => new SyntheticCategoryDescriptor(unit.Key.EvidenceCategory,
                        SyntheticApplicability.RequiresAssessment)).ToArray()))).ToArray();
            var inventory = new SyntheticBaselineInventory(SyntheticBaselineInventoryPlanner.FixtureVersion, preset.Id,
                DemoFixtureCatalog.Scope, SyntheticBaselinePermission.Eligible, compatibility, Array.AsReadOnly(objects));
            yield return new(preset.Id, preset.Label, "Typed synthetic facts evaluated by fixed versioned deterministic fixture rules.",
                capability, inventory, Array.AsReadOnly(planned.ExpectedResults.ToArray()));
        }
    }
    public static IReadOnlyList<CoverageItem> WorkResults(SyntheticRunSnapshot run) => IsAnalysisBaseline(run.BaselineCatalogId)
        ? SyntheticAnalysisEngine.Plan(Freeze(run.BaselineCatalogId, run.ProfileCatalogId)).ExpectedResults
        : DemoFixtureCatalog.Baselines.Single(item => item.Id == run.BaselineCatalogId).ScriptedResults;

    public static bool MatchesFrozenFixture(SyntheticRunSnapshot run)
    {
        if (run.Scope != DemoFixtureCatalog.Scope ||
            !DemoFixtureCatalog.Baselines.Any(item => item.Id == run.BaselineCatalogId) ||
            !DemoFixtureCatalog.Profiles.Any(item => item.Id == run.ProfileCatalogId)) return false;
        if (!Compatible(run.BaselineCatalogId, run.ProfileCatalogId)) return false;
        if (DemoFixtureCatalog.ScriptDigest(run.BaselineCatalogId) != run.FrozenInputs.ScriptedResultsDigest) return false;
        if (!IsAnalysisBaseline(run.BaselineCatalogId)) return run.FrozenInputs.AnalysisFixtureDigest is null;
        var expected = DemoFixtureCatalog.CreateStartRequest(run.BaselineCatalogId, run.ProfileCatalogId, "synthetic-analysis-version-check");
        if (run.FrozenInputs != expected.Versions) return false;
        var expectedPlan = SyntheticBaselineInventoryPlanner.Plan(expected.Capability, expected.Baseline, expected.Scope);
        if (!expectedPlan.HasPlan || SyntheticCanonicalDigest.Compute(run.Plan) != SyntheticCanonicalDigest.Compute(expectedPlan.Plan)) return false;
        var frozen = Freeze(run.BaselineCatalogId, run.ProfileCatalogId);
        return run.FrozenInputs.AnalysisFixtureDigest == SyntheticCanonicalDigest.Compute(frozen) &&
            run.FrozenInputs.ProfileVersion == frozen.ProfileVersion && run.FrozenInputs.ScoringAlgorithmVersion == "pilot-health-v1" &&
            run.Plan.CapabilityLock.RuleCatalogVersion == frozen.CatalogVersion && run.Plan.BaselineId == frozen.PresetId;
    }
}
