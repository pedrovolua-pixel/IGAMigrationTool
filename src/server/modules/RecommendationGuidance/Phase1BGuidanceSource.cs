using System.Text.Json;
using System.Text.Json.Nodes;

namespace RecommendationGuidance;

public static partial class RecommendationGuidanceBuilder
{
    private static GuidanceIssue? ValidatePhase1BSource(GuidanceSourceBinding source)
    {
        const string profile = "synthetic-phase1b-combined-v1";
        if (source.ProfileId != profile || source.BaselineId != "synthetic-phase1b-baseline-v1" || source.RunRevision > 9_007_199_254_740_991)
            return GuidanceIssue.UnknownVersion;
        var versions = source.FrozenVersions;
        string[] fields = ["profileVersion", "desiredOutcomeVersion", "scoringAlgorithmVersion", "aiPolicyVersion", "promptVersion", "modelVersion", "applicationVersion", "workSchemaVersion", "scriptedResultsDigest", "analysisFixtureDigest", "maturityFixtureDigest", "fixPackageTemplateDigest", "fixReviewContractDigest", "planningTaskContractDigest", "phase1bLocks"];
        if (!Object(versions, fields)) return GuidanceIssue.InvalidSource;
        var fixedVersions = new Dictionary<string, string>
        {
            ["profileVersion"] = "synthetic-profile-v1",
            ["desiredOutcomeVersion"] = "synthetic-outcome-lock-v1",
            ["scoringAlgorithmVersion"] = "pilot-health-v1",
            ["aiPolicyVersion"] = "synthetic-automatic-ai-policy-v1",
            ["promptVersion"] = "fixture-prompt-v1",
            ["modelVersion"] = "synthetic-fixed-provider-v1",
            ["applicationVersion"] = "synthetic-phase1b-app-v1",
            ["workSchemaVersion"] = "synthetic-run-work-v1",
            ["fixPackageTemplateDigest"] = "a40f3ccb1128581f36de236dbca3353097f4034b6738bcd01a98275229bee669",
            ["fixReviewContractDigest"] = "a0dca320bcf11dda2f03abc16387f75395caff48e9c917c6b58a2826eb5a8b0f",
            ["planningTaskContractDigest"] = "f4d2c4c4974ac801d9b9a538065f1c1dd89f1519796edb384ea6f0506e027cf2"
        };
        if (fixedVersions.Any(p => String(versions, p.Key) != p.Value)) return GuidanceIssue.UnknownVersion;
        var locks = versions.GetProperty("phase1bLocks");
        if (!Object(locks, ["schemaVersion", "outcomeContractDigest", "priorityPolicyVersion", "aiContractDigest", "csvContractDigest", "outcomeLockDigest", "aiFixtureDigest", "aiMappingDigest", "aiPacketDigest", "fixtureEpoch"])) return GuidanceIssue.InvalidSource;
        var constants = new Dictionary<string, string>
        {
            ["schemaVersion"] = "synthetic-phase1b-input-v1",
            ["priorityPolicyVersion"] = "synthetic-priority-policy-v1",
            ["outcomeContractDigest"] = "f7775ef74297f0b2e563c01d435bf4800fd07a71ea9ce18558cca95a3c7af5b8",
            ["aiContractDigest"] = "471fdcb92780bac8f552366c988eab7d4f7498a123e1625aacc1dd2ecaf49af5",
            ["csvContractDigest"] = "108537455b4f4beac92b0cb24c526d261385662f8ff64dc6af1c5c172ef7ecc7",
            ["fixtureEpoch"] = "synthetic-phase1b-fixture-epoch-v1"
        };
        if (constants.Any(p => String(locks, p.Key) != p.Value) || new[] { "outcomeLockDigest", "aiFixtureDigest", "aiMappingDigest", "aiPacketDigest" }.Any(n => !Digest(String(locks, n)))) return GuidanceIssue.UnknownVersion;
        // Reuse only the unchanged foundation validator against a separate reference envelope.
        // The original new-profile source remains the actual output/digest binding; no old run/source is rewritten or promoted.
        var foundation = JsonNode.Parse(versions.GetRawText())!.AsObject();
        foundation.Remove("phase1bLocks");
        foundation["desiredOutcomeVersion"] = null;
        foundation["aiPolicyVersion"] = "synthetic-ai-disabled-v1";
        foundation["promptVersion"] = "synthetic-prompt-disabled-v1";
        foundation["modelVersion"] = "synthetic-model-disabled-v1";
        foundation["applicationVersion"] = "synthetic-planning-tasks-app-v1";
        using var document = JsonDocument.Parse(foundation.ToJsonString());
        return ValidateSource(source with
        {
            ProfileId = "synthetic-review-maturity-planning-tasks-equal-v1",
            BaselineId = "synthetic-analysis-findings-v1",
            FrozenVersions = document.RootElement.Clone()
        });
    }
}
