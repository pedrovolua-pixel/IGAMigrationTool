using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AssessmentRuns;

// Independently authored primitives from the settled template blueprint, never projected output.
internal static class Expected
{
    internal const string Baseline = "baseline-ai-configuration-v1";
    internal const string Normal = "profile-ai-preview-v1";
    internal const string Empty = "profile-ai-preview-empty-v1";
    internal const string A = "ev-1111111111111111111111111111111111111111111111111111111111111111";
    internal const string B = "ev-2222222222222222222222222222222222222222222222222222222222222222";
    internal const string Schedule = "fixture-rule-schedule-v1";
    internal const string Conflict = "fixture-rule-conflict-v1";
    internal const string Disclaimer = "Fictional offline preview. AI output is proposed and untrusted; cited statements are not verified facts. No evidence is resolved and no action is authorized.";
    internal const string Suggestion = "Review these fictional settings; <script>window.fixtureExecuted=true</script> is data only.";
    internal static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    internal static string Canonical(JsonNode? value)
    {
        if (value is JsonObject obj) return "{" + string.Join(",", obj.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => JsonSerializer.Serialize(pair.Key) + ":" + Canonical(pair.Value))) + "}";
        if (value is JsonArray array) return "[" + string.Join(",", array.Select(Canonical)) + "]";
        return value?.ToJsonString() ?? "null";
    }
    private static JsonArray Ids(params string[] values) => new(values.Order(StringComparer.Ordinal).Select(x => (JsonNode)JsonValue.Create(x)!).ToArray());
    private static JsonObject Statement(string text, string[] evidence, string[] rules) => new() { ["text"] = text, ["evidenceIds"] = Ids(evidence), ["ruleIds"] = Ids(rules) };
    internal static JsonArray Proposals(bool empty)
    {
        if (empty) return new JsonArray();
        return new JsonArray(new JsonObject
        {
            ["proposalId"] = "proposal-01",
            ["facts"] = new JsonArray(Statement("Fictional scheduleEnabled is disabled.", [A], [Schedule]), Statement("Fictional retryPolicy describes three attempts.", [B], [Conflict])),
            ["inferences"] = new JsonArray(Statement("Fictional schedule and retry evidence may conflict.", [B, A], [Schedule, Conflict])),
            ["assumptions"] = new JsonArray(Statement("Fictional context: café, 漢字, 😀.", [A], [Schedule])),
            ["missingContext"] = new JsonArray("Consultant confirmation of the fictional fixture assumptions.", "No actual provider or customer evidence was used."),
            ["suggestions"] = new JsonArray(Statement(Suggestion, [B, A], [Conflict])),
            ["uncertainty"] = "Fictional conflict remains unresolved; no setting is selected.",
            ["conflictingEvidenceIds"] = Ids(B, A)
        });
    }
    internal static (string Packet, string Proposal, string Preview) Full(SyntheticRunSnapshot run, string templateDigest)
        => ForSource(run.RunId.ToString("D"), run.InputDigest, templateDigest, run.ProfileCatalogId == Empty);
    internal static (string Packet, string Proposal, string Preview) ForSource(string runId, string inputDigest, string templateDigest, bool empty)
    {
        var source = new JsonObject
        {
            ["customerId"] = "synthetic-customer",
            ["projectId"] = "synthetic-project",
            ["environmentId"] = "synthetic-environment",
            ["runId"] = runId,
            ["baselineDigest"] = templateDigest,
            ["profileDigest"] = inputDigest,
            ["normalizationVersion"] = "fixture-normalization-v1",
            ["redactionVersion"] = "fixture-redaction-v1",
            ["promptVersion"] = "fixture-prompt-v1"
        };
        JsonObject Record(string id, string key, string value) => new() { ["evidenceId"] = id, ["classification"] = "NormalizedRedactedConfiguration", ["redactionCount"] = 0, ["configurationKey"] = key, ["configurationValue"] = value };
        var packet = Canonical(new JsonObject { ["schemaVersion"] = "synthetic-ai-fixture-packet-v1", ["status"] = "SyntheticDataOnly", ["source"] = source.DeepClone(), ["evidence"] = new JsonArray(Record(A, "scheduleEnabled", "false"), Record(B, "retryPolicy", "three attempts")), ["ruleIds"] = Ids(Schedule, Conflict) });
        var proposals = Proposals(empty);
        var proposal = Canonical(new JsonObject { ["schemaVersion"] = "synthetic-ai-proposal-snapshot-v1", ["status"] = "Proposed", ["runId"] = runId, ["packetDigest"] = Hash(packet), ["proposals"] = proposals.DeepClone() });
        var preview = Canonical(new JsonObject { ["schemaVersion"] = "synthetic-ai-preview-v1", ["status"] = "Proposed", ["source"] = source.DeepClone(), ["packetDigest"] = Hash(packet), ["proposalDigest"] = Hash(proposal), ["proposals"] = proposals, ["disclaimer"] = Disclaimer });
        return (packet, proposal, preview);
    }
    internal static string InputDigest(string planJson, string versionsJson, string baseline, string profile) => Hash(string.Concat(new[] { "synthetic-run-input-lock-v1", planJson, versionsJson, baseline, profile }.Select(x => x.Length + ":" + x)));
    internal static void CheckTemplatePrimitives()
    {
        using var packet = JsonDocument.Parse(DemoAiPreviewCatalog.PacketTemplate);
        var records = packet.RootElement.GetProperty("evidence").EnumerateArray().ToArray();
        Check.Equal(records.Length, 2, "independent-template-record-count");
        Check.Equal(records[0].GetProperty("evidenceId").GetString(), A, "independent-template-first-evidence");
        Check.Equal(records[0].GetProperty("configurationKey").GetString(), "scheduleEnabled", "independent-template-first-key");
        Check.Equal(records[0].GetProperty("configurationValue").GetString(), "false", "independent-template-first-value");
        Check.Equal(records[1].GetProperty("evidenceId").GetString(), B, "independent-template-second-evidence");
        Check.Equal(records[1].GetProperty("configurationKey").GetString(), "retryPolicy", "independent-template-second-key");
        Check.Equal(records[1].GetProperty("configurationValue").GetString(), "three attempts", "independent-template-second-value");
        Check.Equal(Hash(DemoAiPreviewCatalog.PacketTemplate), DemoAiPreviewCatalog.PacketTemplateDigest, "whole-template-byte-digest");
        foreach (var profile in new[] { Normal, Empty })
        {
            var response = JsonNode.Parse(DemoAiPreviewCatalog.ResponseTemplate(profile))!.AsObject();
            foreach (var proposal in response["proposals"]!.AsArray())
            {
                foreach (var category in new[] { "facts", "inferences", "assumptions", "suggestions" })
                    foreach (var statement in proposal![category]!.AsArray())
                        foreach (var field in new[] { "evidenceIds", "ruleIds" })
                            statement![field] = new JsonArray(statement[field]!.AsArray().Select(x => x!.GetValue<string>()).Order(StringComparer.Ordinal).Select(x => (JsonNode)JsonValue.Create(x)!).ToArray());
                proposal!["conflictingEvidenceIds"] = new JsonArray(proposal["conflictingEvidenceIds"]!.AsArray().Select(x => x!.GetValue<string>()).Order(StringComparer.Ordinal).Select(x => (JsonNode)JsonValue.Create(x)!).ToArray());
            }
            Check.Equal(Canonical(response["proposals"]), Canonical(Proposals(profile == Empty)), "entire-literal-response-primitives");
            var digest = Hash("synthetic-ai-demo-fixture-v1\n" + Baseline + "\n" + profile + "\n" + Hash(DemoAiPreviewCatalog.PacketTemplate) + "\n" + Hash(DemoAiPreviewCatalog.ResponseTemplate(profile)));
            Check.Equal(DemoAiPreviewCatalog.FixtureDigest(profile), digest, "independent-complete-fixture-recipe");
        }
    }
}
internal static class Check
{
    internal static int Count;
    internal static string Last = "initialization";
    internal static void That(bool condition, string code) { Last = code; if (!condition) throw new InvalidOperationException(); Count++; }
    internal static void Equal<T>(T actual, T expected, string code) => That(EqualityComparer<T>.Default.Equals(actual, expected), code);
    internal static void Group(string code) => Console.WriteLine("PASS " + code);
}
