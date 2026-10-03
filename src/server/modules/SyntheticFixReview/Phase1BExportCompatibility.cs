using System.Text.Json;

namespace SyntheticFixReview;

public static class Phase1BExportCompatibility
{
    public const string ProfileId = "synthetic-phase1b-combined-v1", ApplicationVersion = "synthetic-phase1b-app-v1";
    public static bool ValidFrozenVersions(JsonElement versions)
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
        return !string.IsNullOrWhiteSpace(locks.GetProperty("fixtureEpoch").GetString()) && versions.GetProperty("applicationVersion").GetString() == ApplicationVersion;
    }
}
