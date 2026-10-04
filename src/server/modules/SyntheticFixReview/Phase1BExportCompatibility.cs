using System.Text.Json;

namespace SyntheticFixReview;

public static class Phase1BExportCompatibility
{
    public const string ProfileId = "synthetic-phase1b-combined-v1", ApplicationVersion = "synthetic-phase1b-app-v1";
    public static bool ValidFrozenVersions(JsonElement versions)
    {
        try
        {
            if (versions.ValueKind != JsonValueKind.Object) return false;
            string[] fields = ["profileVersion", "desiredOutcomeVersion", "scoringAlgorithmVersion", "aiPolicyVersion", "promptVersion", "modelVersion", "applicationVersion", "workSchemaVersion", "scriptedResultsDigest", "analysisFixtureDigest", "maturityFixtureDigest", "fixPackageTemplateDigest", "fixReviewContractDigest", "planningTaskContractDigest", "phase1bLocks"];
            var actual = versions.EnumerateObject().Select(item => item.Name).ToArray();
            if (actual.Length != fields.Length || !actual.Order(StringComparer.Ordinal).SequenceEqual(fields.Order(StringComparer.Ordinal))) return false;
            var locks = versions.GetProperty("phase1bLocks");
            string[] names = ["schemaVersion", "outcomeContractDigest", "priorityPolicyVersion", "aiContractDigest", "csvContractDigest", "outcomeLockDigest", "aiFixtureDigest", "aiMappingDigest", "aiPacketDigest", "fixtureEpoch"];
            if (locks.ValueKind != JsonValueKind.Object || !locks.EnumerateObject().Select(item => item.Name).Order(StringComparer.Ordinal).SequenceEqual(names.Order(StringComparer.Ordinal)) ||
                locks.GetProperty("schemaVersion").GetString() != "synthetic-phase1b-input-v1" ||
                locks.GetProperty("outcomeContractDigest").GetString() != "f7775ef74297f0b2e563c01d435bf4800fd07a71ea9ce18558cca95a3c7af5b8" ||
                locks.GetProperty("priorityPolicyVersion").GetString() != "synthetic-priority-policy-v1" ||
                locks.GetProperty("aiContractDigest").GetString() != "471fdcb92780bac8f552366c988eab7d4f7498a123e1625aacc1dd2ecaf49af5" ||
                locks.GetProperty("csvContractDigest").GetString() != "108537455b4f4beac92b0cb24c526d261385662f8ff64dc6af1c5c172ef7ecc7") return false;
            foreach (var name in new[] { "outcomeLockDigest", "aiFixtureDigest", "aiMappingDigest", "aiPacketDigest" })
                if (!ArtifactReviewPolicy.ValidDigest(locks.GetProperty(name).GetString())) return false;
            return locks.GetProperty("fixtureEpoch").GetString() == "synthetic-phase1b-fixture-epoch-v1" && versions.GetProperty("applicationVersion").GetString() == ApplicationVersion &&
                versions.GetProperty("profileVersion").GetString() == "synthetic-profile-v1" && versions.GetProperty("scoringAlgorithmVersion").GetString() == "pilot-health-v1" &&
                versions.GetProperty("aiPolicyVersion").GetString() == "synthetic-automatic-ai-policy-v1" && versions.GetProperty("promptVersion").GetString() == "fixture-prompt-v1" &&
                versions.GetProperty("modelVersion").GetString() == "synthetic-fixed-provider-v1" && versions.GetProperty("workSchemaVersion").GetString() == "synthetic-run-work-v1" &&
                versions.GetProperty("fixPackageTemplateDigest").GetString() == "a40f3ccb1128581f36de236dbca3353097f4034b6738bcd01a98275229bee669" &&
                versions.GetProperty("fixReviewContractDigest").GetString() == "a0dca320bcf11dda2f03abc16387f75395caff48e9c917c6b58a2826eb5a8b0f" &&
                versions.GetProperty("planningTaskContractDigest").GetString() == "f4d2c4c4974ac801d9b9a538065f1c1dd89f1519796edb384ea6f0506e027cf2";
        }
        catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException or ArgumentException) { return false; }
    }
}
