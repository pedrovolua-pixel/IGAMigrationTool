using System.Collections.Immutable;
using System.Text.Json.Nodes;
using SyntheticAiExecution;
using SyntheticAiValidation;
using DeterministicAnalysis;

namespace AssessmentRuns;

/// <summary>Fixed versioned fictional input and trusted mapping. No user/model-supplied severity, authority or source resolver.</summary>
public static class DemoPhase1BAiFixture
{
    public static string BaselineDigest => SyntheticCanonicalDigest.Compute(DemoPhase1BCatalog.CreateBaseline().Inventory);
    public static string ProfileDigest => SyntheticCanonicalDigest.Compute(DemoPhase1BCatalog.CreateProfile());
    public static ImmutableArray<AiWork> Works(Guid runId)
    {
        var packet = JsonNode.Parse(DemoAiPreviewCatalog.PacketTemplate)!.AsObject();
        packet["source"]!["runId"] = runId.ToString("D");
        packet["source"]!["baselineDigest"] = BaselineDigest;
        packet["source"]!["profileDigest"] = ProfileDigest;
        var input = packet.ToJsonString();
        const string evidence1 = "ev-1111111111111111111111111111111111111111111111111111111111111111";
        const string evidence2 = "ev-2222222222222222222222222222222222222222222222222222222222222222";
        ImmutableArray<AiUnitMapping> units =
        [
            new("proposal-01", new("synthetic-ai-schedule", "configuration"), "SyntheticControl", "SyntheticOperations", "fixture-rule-schedule-v1", "synthetic-rule-v1",
                "Fictional schedule configuration requires review", AiSeverity.High, "Fictional resilience impact", "Unverified fictional likelihood",
                "Fictional schedule configuration", [evidence1]),
            new("proposal-02", new("synthetic-ai-retry", "configuration"), "SyntheticControl", "SyntheticOperations", "fixture-rule-conflict-v1", "synthetic-rule-v1",
                "Fictional retry configuration requires review", AiSeverity.Medium, "Fictional maintainability impact", "Unverified fictional likelihood",
                "Fictional retry configuration", [evidence2])
        ];
        return [new("synthetic-phase1b-configuration-work-v1", "OPERATIONS", input, units)];
    }
    public static string FixtureDigest(Guid runId) => AiExecutionCanonical.Digest(new { schemaVersion = AiExecutionPolicy.FixtureVersion, works = Works(runId) });
    public static string PacketDigest(Guid runId) => SyntheticAiPacketBuilder.Build(Works(runId)[0].PacketInputJson).Packet!.ContentDigest;
    public static SyntheticPhase1BInputLocks Locks(Guid runId, string outcomeLockDigest) => new(DemoPhase1BCatalog.SchemaVersion,
        DemoPhase1BCatalog.OutcomeContractDigest, DemoPhase1BCatalog.PriorityPolicyVersion, DemoPhase1BCatalog.AiContractDigest,
        DemoPhase1BCatalog.CsvContractDigest, outcomeLockDigest, FixtureDigest(runId), AiExecutionPolicy.MappingDigest(Works(runId)), PacketDigest(runId), DemoPhase1BCatalog.FixtureEpoch);
    public static AiRunLock RunLock(SyntheticRunSnapshot run) => new(run.RunId, AiScope.Fixed, DemoPhase1BCatalog.ProfileId,
        DemoPhase1BCatalog.ApplicationVersion, run.InputDigest, BaselineDigest, ProfileDigest, FixtureDigest(run.RunId),
        AiExecutionPolicy.PolicyVersion, AiExecutionPolicy.ProviderVersion, AiExecutionPolicy.PromptVersion,
        AiExecutionPolicy.FixtureVersion, AiExecutionPolicy.MappingDigest(Works(run.RunId)), DemoPhase1BCatalog.FixtureEpoch, "synthetic-consultant");
}
