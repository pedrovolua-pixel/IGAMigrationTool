using System.Text.Json;
using System.Text.Json.Nodes;

namespace SyntheticAiValidation;

public static class SyntheticAiProposalValidator
{
    private static readonly string[] StatementFields = ["facts", "inferences", "assumptions", "suggestions"];

    public static ProposalValidationResult Validate(SyntheticAiPacket? packet, string? outputJson)
    {
        if (packet is null) return Denied(ProposalValidationIssue.MissingPacket);
        if (!FixtureJson.TryParse(outputJson, 256 * 1024, out var document))
            return Denied(ProposalValidationIssue.InvalidJson);
        using (document)
        {
            var root = document!.RootElement;
            if (!FixtureJson.HasOnly(root, "schemaVersion", "runId", "packetDigest", "proposals"))
                return Denied(ProposalValidationIssue.InvalidShape);
            if (!IsString(root.GetProperty("schemaVersion"), "synthetic-ai-fixture-output-v1"))
                return Denied(ProposalValidationIssue.UnsupportedVersion);
            if (packet.RunId == Guid.Empty || !IsString(root.GetProperty("runId"), packet.RunId.ToString("D")) ||
                !FixtureJson.IsDigest(packet.ContentDigest) || !IsString(root.GetProperty("packetDigest"), packet.ContentDigest))
                return Denied(ProposalValidationIssue.PacketMismatch);
            var proposals = root.GetProperty("proposals");
            if (!IsArray(proposals, 0, 16)) return Denied(ProposalValidationIssue.InvalidShape);

            var evidence = packet.EvidenceIds.ToHashSet(StringComparer.Ordinal);
            var rules = packet.RuleIds.ToHashSet(StringComparer.Ordinal);
            var proposalIds = new HashSet<string>(StringComparer.Ordinal);
            var accepted = new List<JsonObject>();
            foreach (var proposal in proposals.EnumerateArray())
            {
                if (!FixtureJson.HasOnly(proposal, "proposalId", "facts", "inferences", "assumptions", "missingContext",
                    "suggestions", "uncertainty", "conflictingEvidenceIds"))
                    return Denied(ProposalValidationIssue.InvalidShape);
                var id = proposal.GetProperty("proposalId");
                if (id.ValueKind != JsonValueKind.String || !IsProposalId(id.GetString()!) || !proposalIds.Add(id.GetString()!))
                    return Denied(ProposalValidationIssue.InvalidProposal);
                var clone = (JsonObject)JsonNode.Parse(proposal.GetRawText())!;
                var statementCount = 0;
                foreach (var field in StatementFields)
                {
                    var statements = proposal.GetProperty(field);
                    if (!IsArray(statements, 0, 16)) return Denied(ProposalValidationIssue.InvalidShape);
                    statementCount += statements.GetArrayLength();
                    foreach (var statement in statements.EnumerateArray())
                    {
                        if (!FixtureJson.HasOnly(statement, "text", "evidenceIds", "ruleIds") ||
                            !IsNonblankString(statement.GetProperty("text")))
                            return Denied(ProposalValidationIssue.InvalidShape);
                        if (!ValidIds(statement.GetProperty("evidenceIds"), evidence, 1, 16) ||
                            !ValidIds(statement.GetProperty("ruleIds"), rules, 1, 2))
                            return Denied(ProposalValidationIssue.InvalidCitation);
                    }
                    foreach (var statement in clone[field]!.AsArray())
                    {
                        SortIds(statement!.AsObject(), "evidenceIds");
                        SortIds(statement.AsObject(), "ruleIds");
                    }
                }
                if (statementCount == 0) return Denied(ProposalValidationIssue.InvalidProposal);
                var missing = proposal.GetProperty("missingContext");
                var uncertainty = proposal.GetProperty("uncertainty");
                if (!IsArray(missing, 0, 16) || !missing.EnumerateArray().All(IsNonblankString) ||
                    uncertainty.ValueKind != JsonValueKind.String)
                    return Denied(ProposalValidationIssue.InvalidShape);
                var conflicts = proposal.GetProperty("conflictingEvidenceIds");
                if (!ValidIds(conflicts, evidence, 0, 16)) return Denied(ProposalValidationIssue.InvalidCitation);
                if (conflicts.GetArrayLength() > 0 &&
                    (conflicts.GetArrayLength() < 2 || !IsNonblankString(uncertainty) || missing.GetArrayLength() == 0))
                    return Denied(ProposalValidationIssue.InvalidConflict);
                SortIds(clone, "conflictingEvidenceIds");
                accepted.Add(clone);
            }

            var snapshot = new JsonObject
            {
                ["schemaVersion"] = "synthetic-ai-proposal-snapshot-v1",
                ["status"] = "Proposed",
                ["runId"] = packet.RunId.ToString("D"),
                ["packetDigest"] = packet.ContentDigest,
                ["proposals"] = new JsonArray(accepted.OrderBy(proposal => proposal["proposalId"]!.GetValue<string>(),
                    StringComparer.Ordinal).Select(proposal => (JsonNode)proposal).ToArray())
            };
            using var snapshotDocument = JsonDocument.Parse(snapshot.ToJsonString());
            var canonical = FixtureJson.Canonical(snapshotDocument.RootElement);
            return ProposalValidationResult.Accepted(new SyntheticAiProposalSnapshot(canonical,
                FixtureJson.Hash(canonical), accepted.Count));
        }
    }

    private static ProposalValidationResult Denied(ProposalValidationIssue issue) => ProposalValidationResult.Denied(issue);

    private static bool IsString(JsonElement value, string expected) =>
        value.ValueKind == JsonValueKind.String && string.Equals(value.GetString(), expected, StringComparison.Ordinal);

    private static bool IsNonblankString(JsonElement value) =>
        value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString());

    private static bool IsArray(JsonElement value, int minimum, int maximum) =>
        value.ValueKind == JsonValueKind.Array && value.GetArrayLength() >= minimum && value.GetArrayLength() <= maximum;

    private static bool IsProposalId(string value) => value.Length == 11 && value.StartsWith("proposal-", StringComparison.Ordinal) &&
        value[9] is >= '0' and <= '9' && value[10] is >= '0' and <= '9';

    private static bool ValidIds(JsonElement values, HashSet<string> allowed, int minimum, int maximum)
    {
        if (!IsArray(values, minimum, maximum)) return false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return values.EnumerateArray().All(value => value.ValueKind == JsonValueKind.String &&
            allowed.Contains(value.GetString()!) && seen.Add(value.GetString()!));
    }

    private static void SortIds(JsonObject value, string field) => value[field] = new JsonArray(value[field]!.AsArray()
        .Select(item => item!.GetValue<string>()).Order(StringComparer.Ordinal).Select(item => (JsonNode?)JsonValue.Create(item)).ToArray());
}
