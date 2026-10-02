using System.Text;
using System.Text.Json;
using AssessmentCoverage;
using AssessmentOrchestration;

namespace AssessmentRuns;

/// <summary>Dedicated fictional configuration and fixed offline response; never a provider or health finding.</summary>
public static class DemoAiPreviewCatalog
{
    public const string BaselineId = "baseline-ai-configuration-v1";
    public const string ProfileId = "profile-ai-preview-v1";
    public const string EmptyProfileId = "profile-ai-preview-empty-v1";
    public static string PacketTemplate { get; } = ReadTemplate("ai-preview-packet.json");
    private static string ProposalsTemplate { get; } = ReadTemplate("ai-preview-proposals.json");
    private static string EmptyTemplate { get; } = ReadTemplate("ai-preview-empty.json");
    public static string PacketTemplateDigest { get; } = SyntheticRunMigration.Hash(PacketTemplate);
    public static bool IsProfile(string? id) => id is ProfileId or EmptyProfileId;
    public static string ResponseTemplate(string profileId) => profileId switch
    {
        ProfileId => ProposalsTemplate,
        EmptyProfileId => EmptyTemplate,
        _ => throw new ArgumentException("Unknown offline AI fixture profile.", nameof(profileId))
    };
    public static string FixtureDigest(string profileId) => SyntheticRunMigration.Hash(
        "synthetic-ai-demo-fixture-v1\n" + BaselineId + "\n" + profileId + "\n" +
        PacketTemplateDigest + "\n" + SyntheticRunMigration.Hash(ResponseTemplate(profileId)));

    public static SyntheticDemoBaseline CreateBaseline()
    {
        var modules = Array.AsReadOnly(new[] { new ModuleVersion("QBM", "synthetic-module-v1") });
        var capability = new CapabilitySnapshot("synthetic-ai-matrix-v1", CapabilityLifecycleState.FixtureVerified,
            "synthetic-ai-configuration-v1", "synthetic-ai-schema-v1", "synthetic-hotfix-digest", "synthetic-sql-build", 160,
            modules, "synthetic-ai-query-pack-v1", "fixture-normalization-v1", "synthetic-ai-fixture-rules-v1");
        var compatibility = new BaselineCompatibility(capability.ProductBuild, capability.DatabaseSchemaBuild,
            capability.HotfixSetDigest, capability.SqlServerBuild, capability.CompatibilityLevel, modules,
            capability.QueryPackVersion, capability.NormalizationSchemaVersion);
        var category = new SyntheticCategoryDescriptor("configuration", SyntheticApplicability.RequiresAssessment);
        var objects = Array.AsReadOnly(new[] { "synthetic-ai-retry", "synthetic-ai-schedule" }.Select(id =>
            new SyntheticInventoryObject(id, "synthetic-configuration", null, "QBM", Array.AsReadOnly(new[] { category }))).ToArray());
        return new(BaselineId, "Fictional configuration · offline AI preview",
            "Two explicitly supplied configuration coverage outcomes. Fixed simulated AI proposals are untrusted; scoring remains paused.",
            capability, new(SyntheticBaselineInventoryPlanner.FixtureVersion, BaselineId, DemoFixtureCatalog.Scope,
                SyntheticBaselinePermission.Eligible, compatibility, objects), Array.AsReadOnly(objects.Select(item =>
                new CoverageItem(new(item.InventoryId, "configuration"), CoverageState.Pass)).ToArray()));
    }

    public static IEnumerable<SyntheticDemoProfile> CreateProfiles()
    {
        yield return Profile(ProfileId, "Fictional configuration · fixed offline AI proposals", "synthetic-ai-preview-profile-v1");
        yield return Profile(EmptyProfileId, "Fictional configuration · empty offline AI response", "synthetic-ai-preview-empty-profile-v1");
    }
    private static SyntheticDemoProfile Profile(string id, string name, string version) => new(id, name,
        new(version, null, "synthetic-scoring-unimplemented-v1", "synthetic-ai-offline-preview-only-v1",
            "fixture-prompt-v1", "synthetic-fixed-response-v1", "synthetic-ai-preview-demo-app-v1",
            SyntheticDurableRunEngine.WorkSchemaVersion, new string('0', 64), AiPreviewFixtureDigest: FixtureDigest(id)));

    /// <summary>Input-only guard: also usable before the worker completes coverage.</summary>
    public static bool MatchesFrozenFixture(SyntheticRunSnapshot? run)
    {
        if (run is null || run.RunId == Guid.Empty || run.Scope != DemoFixtureCatalog.Scope ||
            run.BaselineCatalogId != BaselineId || !IsProfile(run.ProfileCatalogId) || run.Revision < 1 ||
            run.FrozenInputs is null || run.Plan is null || !IsDigest(run.InputDigest)) return false;
        var expected = DemoFixtureCatalog.CreateStartRequest(BaselineId, run.ProfileCatalogId, "offline-ai-source-check");
        if (run.FrozenInputs != expected.Versions) return false;
        var planned = SyntheticBaselineInventoryPlanner.Plan(expected.Capability, expected.Baseline, expected.Scope);
        return planned.HasPlan && JsonSerializer.Serialize(run.Plan) == JsonSerializer.Serialize(planned.Plan) &&
            run.InputDigest == SyntheticDurableRunEngine.ComputeInputDigest(planned.Plan!, expected.Versions, BaselineId, run.ProfileCatalogId);
    }
    private static bool IsDigest(string? value) => value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static string ReadTemplate(string name)
    {
        using var stream = typeof(DemoAiPreviewCatalog).Assembly.GetManifestResourceStream("AssessmentRuns.fixtures." + name)
            ?? throw new InvalidOperationException("Missing fixed offline AI template.");
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), false);
        var value = reader.ReadToEnd();
        if (value.Contains('\r') || value.StartsWith('\uFEFF')) throw new InvalidOperationException("Offline AI templates require UTF-8 without BOM and LF.");
        return value;
    }
}
