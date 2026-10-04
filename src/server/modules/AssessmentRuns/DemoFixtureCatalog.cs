using AssessmentCoverage;
using AssessmentOrchestration;
using System.Text.Json;

namespace AssessmentRuns;

public sealed record SyntheticDemoBaseline(
    string Id, string Name, string Description, CapabilitySnapshot Capability,
    SyntheticBaselineInventory Inventory, IReadOnlyList<CoverageItem> ScriptedResults);
public sealed record SyntheticDemoProfile(string Id, string Name, SyntheticRunInputVersions Versions);

/// <summary>Fixed, value-free, explicitly scripted SYNTHETIC fixtures. A pass here is a supplied outcome, never inferred.</summary>
public static class DemoFixtureCatalog
{
    public static SyntheticAuthorizedScope Scope { get; } = new("synthetic-customer", "synthetic-project", "synthetic-environment");
    public static IReadOnlyList<SyntheticDemoBaseline> Baselines { get; } = Array.AsReadOnly(CreateBaselines().Concat(DemoAnalysisCatalog.CreateBaselines()).Append(DemoAiPreviewCatalog.CreateBaseline()).ToArray());
    public static IReadOnlyList<SyntheticDemoProfile> Profiles { get; } = Array.AsReadOnly(new[]
    {
        new SyntheticDemoProfile("profile-standard", "Synthetic standard profile", Versions("synthetic-profile-v1")),
        new SyntheticDemoProfile("profile-comparison", "Synthetic comparison profile", Versions("synthetic-profile-v2"))
    }.Concat(DemoAnalysisCatalog.CreateProfiles()).Concat(DemoAiPreviewCatalog.CreateProfiles()).ToArray());

    public static SyntheticStartRequest CreateStartRequest(string baselineId, string profileId, string idempotencyKey)
    {
        var baseline = Baselines.SingleOrDefault(item => item.Id == baselineId)
            ?? throw new ArgumentException("Unknown synthetic baseline catalog ID.", nameof(baselineId));
        var profile = Profiles.SingleOrDefault(item => item.Id == profileId)
            ?? throw new ArgumentException("Unknown synthetic profile catalog ID.", nameof(profileId));
        if (!DemoAnalysisCatalog.Compatible(baselineId, profileId))
            throw new ArgumentException("Choose a profile available for this synthetic baseline.", nameof(profileId));
        return new(Scope, idempotencyKey, baseline.Id, profile.Id, baseline.Capability, baseline.Inventory,
            profile.Versions with
            {
                ScriptedResultsDigest = ScriptDigest(baselineId),
                AnalysisFixtureDigest = DemoAnalysisCatalog.IsAnalysisBaseline(baselineId) ? DemoAnalysisCatalog.FrozenDigest(baselineId, profileId) : null,
                MaturityFixtureDigest = DemoAnalysisCatalog.IsReviewMaturityProfile(profileId) ? DemoAnalysisCatalog.FreezeMaturity(baselineId, profileId).ContentDigest : null,
                AiPreviewFixtureDigest = DemoAiPreviewCatalog.IsProfile(profileId) ? DemoAiPreviewCatalog.FixtureDigest(profileId) : null,
                FixPackageTemplateDigest = DemoFixPackageCatalog.IsProfile(profileId) || (DemoArtifactReviewCatalog.IsProfile(profileId) || DemoPlanningTaskCatalog.IsProfile(profileId)) ? DemoFixPackageCatalog.TemplateDigest : null,
                FixReviewContractDigest = DemoArtifactReviewCatalog.IsProfile(profileId) || DemoPlanningTaskCatalog.IsProfile(profileId) ? DemoArtifactReviewCatalog.ContractDigest : null,
                PlanningTaskContractDigest = DemoPlanningTaskCatalog.IsProfile(profileId) ? DemoPlanningTaskCatalog.ContractDigest : null
            });
    }

    public static string ScriptDigest(string baselineId) => ComputeScriptedResultsDigest(
        Baselines.Single(item => item.Id == baselineId).ScriptedResults);

    public static string ComputeScriptedResultsDigest(IEnumerable<CoverageItem> results) => SyntheticRunMigration.Hash(
        JsonSerializer.Serialize(results.OrderBy(item => item.Key.InventoryId, StringComparer.Ordinal)
            .ThenBy(item => item.Key.EvidenceCategory, StringComparer.Ordinal).ToArray()));

    private static SyntheticRunInputVersions Versions(string profile) => new(profile, null,
        "synthetic-scoring-unimplemented-v1", "synthetic-ai-disabled-v1", "synthetic-prompt-disabled-v1",
        "synthetic-model-disabled-v1", "synthetic-app-v1", "synthetic-run-work-v1", new string('0', 64));

    private static IReadOnlyList<SyntheticDemoBaseline> CreateBaselines()
    {
        var modules = Array.AsReadOnly(new[] { new ModuleVersion("QBM", "synthetic-module-v1"), new ModuleVersion("CCC", "synthetic-module-v1") });
        var capability = new CapabilitySnapshot("synthetic-matrix-v1", CapabilityLifecycleState.FixtureVerified,
            "synthetic-product-v1", "synthetic-schema-v1", "synthetic-hotfix-digest", "synthetic-sql-build", 160,
            modules, "synthetic-query-pack-v1", "synthetic-normalization-v1", "synthetic-rule-catalog-v1");
        var compatibility = new BaselineCompatibility(capability.ProductBuild, capability.DatabaseSchemaBuild,
            capability.HotfixSetDigest, capability.SqlServerBuild, capability.CompatibilityLevel, modules,
            capability.QueryPackVersion, capability.NormalizationSchemaVersion);
        var applicable = new SyntheticCategoryDescriptor("generic", SyntheticApplicability.RequiresAssessment);
        var notApplicable = new SyntheticCategoryDescriptor("optional-feature", SyntheticApplicability.NotApplicable,
            "synthetic-feature-not-installed", "synthetic-planner");
        var unsupported = new SyntheticCategoryDescriptor("semantic", SyntheticApplicability.Unsupported,
            "synthetic-semantic-analysis-unavailable", "synthetic-planner");
        var notAssessed = new SyntheticCategoryDescriptor("history", SyntheticApplicability.NotAssessed,
            "synthetic-history-not-assessed", "synthetic-planner");

        SyntheticInventoryObject Object(string id, string module, params SyntheticCategoryDescriptor[] categories) =>
            new(id, "synthetic-native-type", null, module, Array.AsReadOnly(categories));
        SyntheticBaselineInventory Inventory(string id, SyntheticBaselinePermission permission, params SyntheticInventoryObject[] objects) =>
            new(SyntheticBaselineInventoryPlanner.FixtureVersion, id, Scope, permission, compatibility, Array.AsReadOnly(objects));

        var complete = new SyntheticDemoBaseline("baseline-complete", "Synthetic complete coverage",
            "Three explicit scripted outcomes and an explained not-applicable declaration. Scoring remains paused.", capability,
            Inventory("synthetic-baseline-complete-v1", SyntheticBaselinePermission.Eligible,
                Object("synthetic-object-a", "QBM", applicable, notApplicable),
                Object("synthetic-object-b", "QBM", applicable), Object("synthetic-object-c", "CCC", applicable)),
            Array.AsReadOnly(new[]
            {
                new CoverageItem(new("synthetic-object-a", "generic"), CoverageState.Pass),
                new CoverageItem(new("synthetic-object-b", "generic"), CoverageState.Finding),
                new CoverageItem(new("synthetic-object-c", "generic"), CoverageState.Pass)
            }));
        var gaps = new SyntheticDemoBaseline("baseline-gaps", "Synthetic partial evidence",
            "A permission warning and explicit unsupported/not-assessed gaps. Pass is a scripted fixture outcome.", capability,
            Inventory("synthetic-baseline-gaps-v1", SyntheticBaselinePermission.EligibleWithWarning,
                Object("synthetic-object-a", "QBM", applicable, notAssessed),
                Object("synthetic-object-b", "CCC", applicable, unsupported)),
            Array.AsReadOnly(new[]
            {
                new CoverageItem(new("synthetic-object-a", "generic"), CoverageState.Pass),
                new CoverageItem(new("synthetic-object-b", "generic"), CoverageState.Pass)
            }));
        var recoveryObjects = Enumerable.Range(1, 40).Select(index => Object($"synthetic-recovery-{index:D3}", "QBM", applicable)).ToArray();
        var recovery = new SyntheticDemoBaseline("baseline-recovery", "Synthetic recovery demonstration",
            "Forty explicit scripted outcomes make cancellation and restart progress observable. Scoring remains paused.", capability,
            Inventory("synthetic-baseline-recovery-v1", SyntheticBaselinePermission.Eligible, recoveryObjects),
            Array.AsReadOnly(recoveryObjects.Select((item, index) => new CoverageItem(new(item.InventoryId, "generic"),
                index == 7 ? CoverageState.Finding : CoverageState.Pass)).ToArray()));
        var unavailable = new SyntheticDemoBaseline("baseline-not-applicable", "Synthetic zero applicable coverage",
            "Explicit not-applicable units show unavailable executable coverage, never 100 percent.", capability,
            Inventory("synthetic-baseline-not-applicable-v1", SyntheticBaselinePermission.Eligible,
                Object("synthetic-optional", "QBM", notApplicable)), Array.AsReadOnly(Array.Empty<CoverageItem>()));
        return Array.AsReadOnly(new[] { complete, gaps, recovery, unavailable });
    }
}
