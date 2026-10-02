using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using SyntheticAiValidation;

namespace SyntheticAiPreview;

public static class SyntheticAiPreviewBuilder
{
    public static PreviewResult Build(SyntheticAiPacket? packet, SyntheticAiProposalSnapshot? proposal)
    {
        if (packet is null) return Denied(PreviewIssue.MissingPacket);
        if (proposal is null) return Denied(PreviewIssue.MissingProposal);
        if (!Integrity(packet.CanonicalJson, packet.ContentDigest) ||
            !Integrity(proposal.CanonicalJson, proposal.ContentDigest)) return Denied(PreviewIssue.IntegrityMismatch);
        try
        {
            using var packetDocument = JsonDocument.Parse(packet.CanonicalJson, new JsonDocumentOptions { MaxDepth = 16 });
            using var proposalDocument = JsonDocument.Parse(proposal.CanonicalJson, new JsonDocumentOptions { MaxDepth = 16 });
            var packetRoot = packetDocument.RootElement;
            var proposalRoot = proposalDocument.RootElement;
            if (!ValidTree(packetRoot) || !ValidTree(proposalRoot) ||
                !Only(packetRoot, "schemaVersion", "status", "source", "evidence", "ruleIds") ||
                !Equal(packetRoot.GetProperty("schemaVersion"), "synthetic-ai-fixture-packet-v1") ||
                !Equal(packetRoot.GetProperty("status"), "SyntheticDataOnly") ||
                !Only(proposalRoot, "schemaVersion", "status", "runId", "packetDigest", "proposals") ||
                !Equal(proposalRoot.GetProperty("schemaVersion"), "synthetic-ai-proposal-snapshot-v1") ||
                !Equal(proposalRoot.GetProperty("status"), "Proposed")) return Denied(PreviewIssue.InvalidInput);

            var sourceValue = packetRoot.GetProperty("source");
            if (!Only(sourceValue, "customerId", "projectId", "environmentId", "runId", "baselineDigest", "profileDigest",
                "normalizationVersion", "redactionVersion", "promptVersion") ||
                !Equal(sourceValue.GetProperty("customerId"), "synthetic-customer") ||
                !Equal(sourceValue.GetProperty("projectId"), "synthetic-project") ||
                !Equal(sourceValue.GetProperty("environmentId"), "synthetic-environment") ||
                !Equal(sourceValue.GetProperty("normalizationVersion"), "fixture-normalization-v1") ||
                !Equal(sourceValue.GetProperty("redactionVersion"), "fixture-redaction-v1") ||
                !Equal(sourceValue.GetProperty("promptVersion"), "fixture-prompt-v1") ||
                !IsDigest(sourceValue.GetProperty("baselineDigest")) || !IsDigest(sourceValue.GetProperty("profileDigest")) ||
                packet.RunId == Guid.Empty || !Equal(sourceValue.GetProperty("runId"), packet.RunId.ToString("D")) ||
                !Equal(proposalRoot.GetProperty("runId"), packet.RunId.ToString("D")) ||
                !Equal(proposalRoot.GetProperty("packetDigest"), packet.ContentDigest)) return Denied(PreviewIssue.SourceMismatch);

            var evidenceValue = packetRoot.GetProperty("evidence");
            var rulesValue = packetRoot.GetProperty("ruleIds");
            if (!Array(evidenceValue, 1, 16) || !Array(rulesValue, 1, 2) ||
                packet.EvidenceIds.IsDefault || packet.RuleIds.IsDefault) return Denied(PreviewIssue.InvalidInput);
            var evidenceIds = ImmutableArray.CreateBuilder<string>();
            foreach (var evidence in evidenceValue.EnumerateArray())
            {
                if (!Only(evidence, "evidenceId", "classification", "redactionCount", "configurationKey", "configurationValue") ||
                    !Text(evidence.GetProperty("evidenceId")) ||
                    !Equal(evidence.GetProperty("classification"), "NormalizedRedactedConfiguration") ||
                    evidence.GetProperty("redactionCount").ValueKind != JsonValueKind.Number ||
                    !evidence.GetProperty("redactionCount").TryGetInt32(out var count) || count is < 0 or > 1000000 ||
                    !Text(evidence.GetProperty("configurationKey")) ||
                    evidence.GetProperty("configurationKey").GetString() is not ("scheduleEnabled" or "retryPolicy") ||
                    !Text(evidence.GetProperty("configurationValue"))) return Denied(PreviewIssue.InvalidInput);
                var id = evidence.GetProperty("evidenceId").GetString()!;
                if (!id.StartsWith("ev-", StringComparison.Ordinal) || !Digest(id[3..])) return Denied(PreviewIssue.InvalidInput);
                evidenceIds.Add(id);
            }
            if (!rulesValue.EnumerateArray().All(value => Text(value) &&
                value.GetString() is "fixture-rule-schedule-v1" or "fixture-rule-conflict-v1")) return Denied(PreviewIssue.InvalidInput);
            var ruleIds = Strings(rulesValue);
            if (!SortedUnique(evidenceIds) || !SortedUnique(ruleIds) ||
                !packet.EvidenceIds.SequenceEqual(evidenceIds, StringComparer.Ordinal) ||
                !packet.RuleIds.SequenceEqual(ruleIds, StringComparer.Ordinal)) return Denied(PreviewIssue.SourceMismatch);

            var proposalsValue = proposalRoot.GetProperty("proposals");
            if (!Array(proposalsValue, 0, 16) || proposalsValue.GetArrayLength() != proposal.ProposalCount)
                return Denied(PreviewIssue.SourceMismatch);
            var proposals = ImmutableArray.CreateBuilder<PreviewProposal>();
            var allowedEvidence = evidenceIds.ToHashSet(StringComparer.Ordinal);
            var allowedRules = ruleIds.ToHashSet(StringComparer.Ordinal);
            foreach (var value in proposalsValue.EnumerateArray())
            {
                if (!Only(value, "proposalId", "facts", "inferences", "assumptions", "missingContext", "suggestions",
                    "uncertainty", "conflictingEvidenceIds") || !Text(value.GetProperty("proposalId")))
                    return Denied(PreviewIssue.InvalidInput);
                var id = value.GetProperty("proposalId").GetString()!;
                if (id.Length != 11 || !id.StartsWith("proposal-", StringComparison.Ordinal) ||
                    id[9] is < '0' or > '9' || id[10] is < '0' or > '9') return Denied(PreviewIssue.InvalidInput);
                if (!Statements(value.GetProperty("facts"), allowedEvidence, allowedRules, out var facts) ||
                    !Statements(value.GetProperty("inferences"), allowedEvidence, allowedRules, out var inferences) ||
                    !Statements(value.GetProperty("assumptions"), allowedEvidence, allowedRules, out var assumptions) ||
                    !Statements(value.GetProperty("suggestions"), allowedEvidence, allowedRules, out var suggestions) ||
                    facts.Length + inferences.Length + assumptions.Length + suggestions.Length == 0 ||
                    !Array(value.GetProperty("missingContext"), 0, 16) ||
                    !value.GetProperty("missingContext").EnumerateArray().All(Nonblank) || !Text(value.GetProperty("uncertainty")) ||
                    !Ids(value.GetProperty("conflictingEvidenceIds"), allowedEvidence, 0, 16, out var conflicts))
                    return Denied(PreviewIssue.InvalidInput);
                var missing = Strings(value.GetProperty("missingContext"));
                var uncertainty = value.GetProperty("uncertainty").GetString()!;
                if (conflicts.Length > 0 && (conflicts.Length < 2 || string.IsNullOrWhiteSpace(uncertainty) || missing.Length == 0))
                    return Denied(PreviewIssue.InvalidInput);
                proposals.Add(new PreviewProposal(id, facts, inferences, assumptions, missing, suggestions, uncertainty, conflicts));
            }
            if (!SortedUnique(proposals.Select(value => value.ProposalId))) return Denied(PreviewIssue.InvalidInput);
            var source = new PreviewSource(sourceValue.GetProperty("customerId").GetString()!,
                sourceValue.GetProperty("projectId").GetString()!, sourceValue.GetProperty("environmentId").GetString()!,
                packet.RunId, sourceValue.GetProperty("baselineDigest").GetString()!, sourceValue.GetProperty("profileDigest").GetString()!,
                sourceValue.GetProperty("normalizationVersion").GetString()!, sourceValue.GetProperty("redactionVersion").GetString()!,
                sourceValue.GetProperty("promptVersion").GetString()!);
            var immutable = proposals.ToImmutable();
            var canonical = PreviewCanonical.Payload(source, packet.ContentDigest, proposal.ContentDigest, immutable);
            if (Encoding.UTF8.GetByteCount(canonical) > PreviewCanonical.MaximumBytes) return Denied(PreviewIssue.InvalidInput);
            return PreviewResult.Accepted(new SyntheticAiPreviewSnapshot(canonical, PreviewCanonical.Digest(canonical), source,
                packet.ContentDigest, proposal.ContentDigest, immutable));
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
        {
            return Denied(PreviewIssue.InvalidInput);
        }
    }

    private static PreviewResult Denied(PreviewIssue issue) => PreviewResult.Denied(issue);
    private static bool Integrity(string text, string digest)
    {
        try
        {
            return text is not null && Digest(digest) && new UTF8Encoding(false, true).GetByteCount(text) <= PreviewCanonical.MaximumBytes &&
                string.Equals(PreviewCanonical.Digest(text), digest, StringComparison.Ordinal);
        }
        catch (EncoderFallbackException) { return false; }
    }

    private static bool Only(JsonElement value, params string[] names) => value.ValueKind == JsonValueKind.Object &&
        value.EnumerateObject().Count() == names.Length && value.EnumerateObject().All(item => names.Contains(item.Name, StringComparer.Ordinal));
    private static bool Text(JsonElement value) => value.ValueKind == JsonValueKind.String;
    private static bool Nonblank(JsonElement value) => Text(value) && !string.IsNullOrWhiteSpace(value.GetString());
    private static bool Equal(JsonElement value, string expected) => Text(value) && value.GetString() == expected;
    private static bool Digest(string? value) => value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static bool IsDigest(JsonElement value) => Text(value) && Digest(value.GetString());
    private static bool Array(JsonElement value, int minimum, int maximum) => value.ValueKind == JsonValueKind.Array &&
        value.GetArrayLength() >= minimum && value.GetArrayLength() <= maximum;
    private static ImmutableArray<string> Strings(JsonElement value) => value.EnumerateArray().Select(item => item.GetString()!).ToImmutableArray();
    private static bool SortedUnique(IEnumerable<string> values)
    {
        var previous = (string?)null;
        foreach (var value in values)
        {
            if (previous is not null && string.CompareOrdinal(previous, value) >= 0) return false;
            previous = value;
        }
        return true;
    }

    private static bool Ids(JsonElement value, HashSet<string> allowed, int minimum, int maximum, out ImmutableArray<string> ids)
    {
        ids = [];
        if (!Array(value, minimum, maximum) || !value.EnumerateArray().All(item => Text(item) && allowed.Contains(item.GetString()!))) return false;
        ids = Strings(value);
        return SortedUnique(ids);
    }

    private static bool Statements(JsonElement value, HashSet<string> evidence, HashSet<string> rules,
        out ImmutableArray<PreviewStatement> statements)
    {
        statements = [];
        if (!Array(value, 0, 16)) return false;
        var builder = ImmutableArray.CreateBuilder<PreviewStatement>();
        foreach (var statement in value.EnumerateArray())
        {
            if (!Only(statement, "text", "evidenceIds", "ruleIds") || !Nonblank(statement.GetProperty("text")) ||
                !Ids(statement.GetProperty("evidenceIds"), evidence, 1, 16, out var evidenceIds) ||
                !Ids(statement.GetProperty("ruleIds"), rules, 1, 2, out var ruleIds)) return false;
            builder.Add(new PreviewStatement(statement.GetProperty("text").GetString()!, evidenceIds, ruleIds));
        }
        statements = builder.ToImmutable();
        return true;
    }

    private static bool ValidTree(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                var seen = new HashSet<string>(StringComparer.Ordinal);
                return value.EnumerateObject().All(item => ValidString(item.Name) && seen.Add(item.Name) && ValidTree(item.Value));
            case JsonValueKind.Array: return value.EnumerateArray().All(ValidTree);
            case JsonValueKind.String: return ValidString(value.GetString()!);
            default: return true;
        }
    }

    private static bool ValidString(string value)
    {
        if (value.Length > 4096) return false;
        for (var index = 0; index < value.Length; index++)
        {
            if (char.IsHighSurrogate(value[index]))
            {
                if (++index >= value.Length || !char.IsLowSurrogate(value[index])) return false;
            }
            else if (char.IsLowSurrogate(value[index])) return false;
        }
        return true;
    }
}
