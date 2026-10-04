using System.Collections.Immutable;
using System.Text.Json;

namespace SyntheticAiValidation;

public static class SyntheticAiPacketBuilder
{
    public static PacketBuildResult Build(string? inputJson)
    {
        if (!FixtureJson.TryParse(inputJson, 64 * 1024, out var document))
            return Failed(PacketIssue.InvalidInput);
        using (document)
        {
            var input = document!.RootElement;
            if (!FixtureJson.HasOnly(input, "schemaVersion", "source", "evidence", "ruleIds"))
                return Failed(PacketIssue.InvalidInput);
            if (!IsString(input, "schemaVersion", out var schema)) return Failed(PacketIssue.InvalidInput);
            if (schema != "synthetic-ai-fixture-input-v1") return Failed(PacketIssue.UnknownVersion);

            var source = input.GetProperty("source");
            if (!FixtureJson.HasOnly(source, "customerId", "projectId", "environmentId", "runId",
                "baselineDigest", "profileDigest", "normalizationVersion", "redactionVersion", "promptVersion"))
                return Failed(PacketIssue.InvalidSource);
            if (!IsString(source, "customerId", out var customer) ||
                !IsString(source, "projectId", out var project) ||
                !IsString(source, "environmentId", out var environment)) return Failed(PacketIssue.InvalidSource);
            if (customer != "synthetic-customer" || project != "synthetic-project" || environment != "synthetic-environment")
                return Failed(PacketIssue.WrongScope);
            if (!IsString(source, "runId", out var runText) ||
                !Guid.TryParseExact(runText, "D", out var runId) || runId == Guid.Empty || runText != runId.ToString("D") ||
                !IsString(source, "baselineDigest", out var baseline) || !FixtureJson.IsDigest(baseline) ||
                !IsString(source, "profileDigest", out var profile) || !FixtureJson.IsDigest(profile))
                return Failed(PacketIssue.InvalidSource);
            if (!IsString(source, "normalizationVersion", out var normalization) ||
                !IsString(source, "redactionVersion", out var redaction) ||
                !IsString(source, "promptVersion", out var prompt)) return Failed(PacketIssue.InvalidSource);
            if (normalization != "fixture-normalization-v1" || redaction != "fixture-redaction-v1" || prompt != "fixture-prompt-v1")
                return Failed(PacketIssue.UnknownVersion);

            var evidence = input.GetProperty("evidence");
            if (evidence.ValueKind != JsonValueKind.Array || evidence.GetArrayLength() is < 1 or > 16)
                return Failed(PacketIssue.InvalidEvidence);
            var evidenceIds = new HashSet<string>(StringComparer.Ordinal);
            var records = new List<JsonElement>();
            foreach (var record in evidence.EnumerateArray())
            {
                if (!FixtureJson.HasOnly(record, "evidenceId", "classification", "redactionCount", "configurationKey", "configurationValue") ||
                    !IsString(record, "evidenceId", out var id) || !id.StartsWith("ev-", StringComparison.Ordinal) ||
                    !FixtureJson.IsDigest(id[3..]) || !IsString(record, "classification", out var classification) ||
                    classification != "NormalizedRedactedConfiguration" ||
                    record.GetProperty("redactionCount").ValueKind != JsonValueKind.Number ||
                    !record.GetProperty("redactionCount").TryGetInt32(out var count) || count is < 0 or > 1000000 ||
                    !IsString(record, "configurationKey", out var key) || key is not ("scheduleEnabled" or "retryPolicy") ||
                    !IsString(record, "configurationValue", out _)) return Failed(PacketIssue.InvalidEvidence);
                if (!evidenceIds.Add(id)) return Failed(PacketIssue.DuplicateId);
                records.Add(record);
            }

            var rules = input.GetProperty("ruleIds");
            if (rules.ValueKind != JsonValueKind.Array || rules.GetArrayLength() is < 1 or > 2)
                return Failed(PacketIssue.InvalidRule);
            var ruleIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rule in rules.EnumerateArray())
            {
                if (rule.ValueKind != JsonValueKind.String || rule.GetString() is not
                    ("fixture-rule-schedule-v1" or "fixture-rule-conflict-v1")) return Failed(PacketIssue.InvalidRule);
                if (!ruleIds.Add(rule.GetString()!)) return Failed(PacketIssue.DuplicateId);
            }

            var sortedEvidence = records.OrderBy(record => record.GetProperty("evidenceId").GetString(), StringComparer.Ordinal).ToArray();
            var sortedRules = ruleIds.Order(StringComparer.Ordinal).ToImmutableArray();
            var packetValue = JsonSerializer.SerializeToElement(new
            {
                schemaVersion = "synthetic-ai-fixture-packet-v1",
                status = "SyntheticDataOnly",
                source,
                evidence = sortedEvidence,
                ruleIds = sortedRules
            });
            var canonicalJson = FixtureJson.Canonical(packetValue);
            return new PacketBuildResult(null, new SyntheticAiPacket(canonicalJson, FixtureJson.Hash(canonicalJson), runId,
                evidenceIds.Order(StringComparer.Ordinal).ToImmutableArray(), sortedRules));
        }
    }

    private static bool IsString(JsonElement value, string name, out string text)
    {
        var property = value.GetProperty(name);
        text = property.ValueKind == JsonValueKind.String ? property.GetString()! : string.Empty;
        return property.ValueKind == JsonValueKind.String;
    }

    private static PacketBuildResult Failed(PacketIssue issue) => new(issue, null);
}
