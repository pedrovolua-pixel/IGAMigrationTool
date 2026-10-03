using System.Text.Json;
using AssessmentCoverage;
using AssessmentOrchestration;
using AssessmentMaturity;
using DeterministicAnalysis;

namespace AssessmentRuns;

/// <summary>Exactly one new opt-in fictional composition. No historical catalog entry is rewritten.</summary>
public static class DemoPhase1BCatalog
{
    public const string BaselineId = "synthetic-phase1b-baseline-v1";
    public const string ProfileId = "synthetic-phase1b-combined-v1";
    public const string ApplicationVersion = "synthetic-phase1b-app-v1";
    public const string SchemaVersion = "synthetic-phase1b-input-v1";
    public const string DeterministicBaselineId = "synthetic-analysis-findings-v1";
    public const string DeterministicProfileId = "synthetic-analysis-equal-v1";
    public const string OutcomeContractDigest = "f7775ef74297f0b2e563c01d435bf4800fd07a71ea9ce18558cca95a3c7af5b8";
    public const string AiContractDigest = "471fdcb92780bac8f552366c988eab7d4f7498a123e1625aacc1dd2ecaf49af5";
    public const string CsvContractDigest = "108537455b4f4beac92b0cb24c526d261385662f8ff64dc6af1c5c172ef7ecc7";
    public const string PriorityPolicyVersion = "synthetic-priority-policy-v1";
    public const string FixtureEpoch = "synthetic-phase1b-fixture-epoch-v1";
    public static readonly Guid RegistryFenceId = Guid.Parse("1b000000-0000-4000-8000-000000000001");
    public static bool IsProfile(string? id) => id == ProfileId;
    public static SyntheticAnalysisLock DeterministicLock => DemoAnalysisCatalog.Freeze(DeterministicBaselineId, DeterministicProfileId);
    public static SyntheticAnalysisPlan DeterministicPlan => SyntheticAnalysisEngine.Plan(DeterministicLock);
    public static string DeterministicDigest => SyntheticCanonicalDigest.Compute(DeterministicLock);
    public static SyntheticMaturityFrozenFixture Maturity => SyntheticMaturityFixturePack.Freeze(DeterministicBaselineId, DeterministicDigest);
    public static IReadOnlyList<CoverageKey> AiKeys { get; } = Array.AsReadOnly(new[]
    { new CoverageKey("synthetic-ai-retry", "configuration"), new CoverageKey("synthetic-ai-schedule", "configuration") });
    public static SyntheticDemoBaseline CreateBaseline()
    {
        var original = DemoAnalysisCatalog.CreateBaselines().Single(b => b.Id == DeterministicBaselineId);
        var ai = AiKeys.Select(key => new SyntheticInventoryObject(key.InventoryId, "synthetic-configuration", null,
            "SyntheticOperations", Array.AsReadOnly(new[] { new SyntheticCategoryDescriptor(key.EvidenceCategory, SyntheticApplicability.RequiresAssessment) })));
        return new(BaselineId, "Phase 1B · deterministic and automatic fictional AI",
            "One fixed synthetic inventory, approved outcome locks, automatic fake AI, planning and safe CSV. No live provider or customer authority.",
            original.Capability, original.Inventory with { BaselineId = BaselineId, Objects = Array.AsReadOnly(original.Inventory.Objects.Concat(ai).ToArray()) },
            DeterministicPlan.ExpectedResults);
    }
    public static SyntheticDemoProfile CreateProfile() => new(ProfileId, "Phase 1B · complete synthetic assessment",
        new("synthetic-profile-v1", "synthetic-outcome-lock-v1", "pilot-health-v1", "synthetic-automatic-ai-policy-v1",
            "fixture-prompt-v1", "synthetic-fixed-provider-v1", ApplicationVersion, SyntheticDurableRunEngine.WorkSchemaVersion,
            DemoFixtureCatalog.ComputeScriptedResultsDigest(DeterministicPlan.ExpectedResults), DeterministicDigest, Maturity.ContentDigest,
            FixPackageTemplateDigest: DemoFixPackageCatalog.TemplateDigest, FixReviewContractDigest: DemoArtifactReviewCatalog.ContractDigest,
            PlanningTaskContractDigest: DemoPlanningTaskCatalog.ContractDigest));
    public static SyntheticStartRequest CreateStartRequest(string requestId, SyntheticPhase1BInputLocks locks,
        string policyVersion, string promptVersion, string providerVersion)
    {
        if (!ValidLocks(locks) || policyVersion != "synthetic-automatic-ai-policy-v1" || promptVersion != "fixture-prompt-v1" || providerVersion != "synthetic-fixed-provider-v1") throw new ArgumentException("Missing or foreign Phase1B locks.");
        var baseline = CreateBaseline();
        var versions = CreateProfile().Versions with { Phase1BLocks = locks, AiPolicyVersion = policyVersion, PromptVersion = promptVersion, ModelVersion = providerVersion };
        return new(DemoFixtureCatalog.Scope, requestId, BaselineId, ProfileId, baseline.Capability, baseline.Inventory, versions);
    }
    public static bool ValidLocks(SyntheticPhase1BInputLocks? locks) => locks is not null &&
        locks.SchemaVersion == SchemaVersion && locks.OutcomeContractDigest == OutcomeContractDigest &&
        locks.PriorityPolicyVersion == PriorityPolicyVersion && locks.AiContractDigest == AiContractDigest &&
        locks.CsvContractDigest == CsvContractDigest && locks.FixtureEpoch == FixtureEpoch &&
        new[] { locks.OutcomeLockDigest, locks.AiFixtureDigest, locks.AiMappingDigest, locks.AiPacketDigest }.All(Digest);
    public static bool MatchesFrozenFixture(SyntheticRunSnapshot? run)
    {
        if (run is null || run.Scope != DemoFixtureCatalog.Scope || run.BaselineCatalogId != BaselineId || !IsProfile(run.ProfileCatalogId) ||
            !ValidLocks(run.FrozenInputs.Phase1BLocks) || run.FrozenInputs.AnalysisFixtureDigest != DeterministicDigest ||
            run.FrozenInputs.MaturityFixtureDigest != Maturity.ContentDigest) return false;
        var expected = CreateStartRequest(run.RunId.ToString("D"), run.FrozenInputs.Phase1BLocks!,
            run.FrozenInputs.AiPolicyVersion, run.FrozenInputs.PromptVersion, run.FrozenInputs.ModelVersion);
        if (JsonSerializer.Serialize(run.FrozenInputs) != JsonSerializer.Serialize(expected.Versions)) return false;
        var plan = SyntheticBaselineInventoryPlanner.Plan(expected.Capability, expected.Baseline, expected.Scope);
        return plan.HasPlan && JsonSerializer.Serialize(run.Plan) == JsonSerializer.Serialize(plan.Plan) &&
            run.InputDigest == SyntheticDurableRunEngine.ComputeInputDigest(plan.Plan!, expected.Versions, BaselineId, ProfileId);
    }
    private static bool Digest(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
