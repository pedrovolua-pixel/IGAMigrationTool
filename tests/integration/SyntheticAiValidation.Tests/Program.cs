using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SyntheticAiValidation;

internal static class Program
{
    private static int Main()
    {
        try
        {
            Composition();
            PacketDenials();
            ProposalDenials();
            BoundsAndHostileData();
            Console.WriteLine($"{Check.Count} independent composed synthetic AI assertions passed. Fixed fake provider only; live AI, authorization/redaction semantics, factual accuracy, budget/recovery and rendering NOT VERIFIED.");
            return 0;
        }
        catch (Exception exception)
        {
            // Never echo fixture/provider strings, parser exception text or stack lines.
            Console.Error.WriteLine($"FAIL V8 {exception.GetType().Name}; {Check.Count} assertions reached; supplied payload omitted.");
            return 1;
        }
    }
    private static SyntheticAiPacket Packet(string? input)
    {
        var result = SyntheticAiPacketBuilder.Build(input);
        Check.That(result.Succeeded && result.Issue is null && result.Packet is not null, "valid independent packet accepted");
        return result.Packet!;
    }
    private static SyntheticAiProposalSnapshot Proposal(SyntheticAiPacket packet, string? output)
    {
        var result = SyntheticAiProposalValidator.Validate(packet, output);
        Check.That(result.Succeeded && result.Issue is null && result.Snapshot is not null, "valid independent proposed output accepted");
        return result.Snapshot!;
    }
    private static void DenyPacket(string? input)
    {
        var result = SyntheticAiPacketBuilder.Build(input);
        Check.That(!result.Succeeded && result.Issue is not null && result.Packet is null, "packet typed payload-free closed denial");
        Check.That(result.Issue!.ToString()!.Length < 128 && !result.Issue.ToString()!.Contains("evil", StringComparison.Ordinal), "packet issue is finite payload-free code");
    }
    private static void DenyProposal(SyntheticAiPacket? packet, string? output)
    {
        var result = SyntheticAiProposalValidator.Validate(packet, output);
        Check.That(!result.Succeeded && result.Issue is not null && result.Snapshot is null, "proposed output typed payload-free closed denial");
        Check.That(result.Issue!.ToString()!.Length < 128 && !result.Issue.ToString()!.Contains("evil", StringComparison.Ordinal), "proposal issue is finite payload-free code");
    }
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static void Composition()
    {
        var input = Fixture.Input(); var packet = Packet(Fixture.Serialize(input));
        Check.Equal(packet.CanonicalJson, Fixture.Read("packet-golden.json"), "independent complete literal packet canonical bytes");
        Check.Equal(packet.ContentDigest, Fixture.PacketHash, "independent fixed packet SHA256");
        Check.Equal(Hash(Fixture.Read("packet-golden.json")), Fixture.PacketHash, "fixed packet oracle self-integrity independent SHA");
        Check.Equal(packet.RunId, Guid.Parse(Fixture.RunId), "literal fixed run identity");
        Check.That(packet.EvidenceIds.SequenceEqual(new[] { Fixture.EvidenceA, Fixture.EvidenceB }), "minimized sorted evidence IDs");
        Check.That(packet.RuleIds.SequenceEqual(new[] { "fixture-rule-conflict-v1", "fixture-rule-schedule-v1" }), "fixed sorted fictional authoritative rule IDs");
        var provider = new FakeProvider(Fixture.Read("provider-output.json"));
        var snapshot = Proposal(packet, provider.Respond());
        Check.Equal(provider.Calls, 1, "fixed provider called once, no client/tool dispatch");
        Check.Equal(snapshot.CanonicalJson, Fixture.Read("proposal-golden.json"), "independent complete literal proposed snapshot bytes");
        Check.Equal(snapshot.ContentDigest, Fixture.ProposalHash, "independent fixed proposed output SHA256");
        Check.Equal(Hash(Fixture.Read("proposal-golden.json")), Fixture.ProposalHash, "fixed proposal oracle self-integrity independent SHA");
        Check.Equal(snapshot.ProposalCount, 2, "literal proposal count");
        using (var value = JsonDocument.Parse(snapshot.CanonicalJson))
        {
            var root = value.RootElement;
            Check.Equal(root.GetProperty("status").GetString(), "Proposed", "every accepted value remains proposed untrusted");
            Check.Equal(root.GetProperty("schemaVersion").GetString(), "synthetic-ai-proposal-snapshot-v1", "literal snapshot schema");
            var first = root.GetProperty("proposals")[0];
            Check.Equal(first.GetProperty("proposalId").GetString(), "proposal-01", "stable proposal identity order");
            foreach (var field in new[] { "facts", "inferences", "assumptions", "suggestions" })
                Check.Equal(first.GetProperty(field).GetArrayLength(), 1, "typed statement groups remain distinct");
            Check.Equal(first.GetProperty("uncertainty").GetString(), "Fixture states conflict; no preference is selected.", "literal conflict uncertainty preserved");
            Check.That(first.GetProperty("conflictingEvidenceIds").EnumerateArray().Select(x => x.GetString()).SequenceEqual(new[] { Fixture.EvidenceA, Fixture.EvidenceB }), "both literal conflicting references preserved");
            Check.That(first.GetProperty("missingContext").EnumerateArray().Select(x => x.GetString()).SequenceEqual(new[] { "Fresh fixture context is missing.", "No customer configuration is supplied." }), "ordered literal missing context preserved");
        }
        var retainedPacket = packet.CanonicalJson; var retainedSnapshot = snapshot.CanonicalJson;
        input["evidence"]![0]!["configurationValue"] = "changed caller-owned JSON";
        Check.Equal(packet.CanonicalJson, retainedPacket, "caller parsed object mutation leaves accepted packet detached");
        var output = Fixture.Output(); output["proposals"]![0]!["facts"]![0]!["text"] = "caller mutation";
        Check.Equal(snapshot.CanonicalJson, retainedSnapshot, "caller/provider mutation leaves accepted proposed output detached");
        for (var i = 0; i < 5; i++)
        {
            var repeated = Proposal(packet, provider.Respond());
            Check.Equal(repeated.CanonicalJson, retainedSnapshot, "repeat fixed response validates reproducibly without side effect");
            Check.Equal(repeated.ContentDigest, Fixture.ProposalHash, "repeat value digest stable");
        }
        var reorderedInput = Fixture.Input(); Reverse(reorderedInput["evidence"]!.AsArray()); Reverse(reorderedInput["ruleIds"]!.AsArray());
        Check.Equal(Packet(ReverseProperties(reorderedInput).ToJsonString()).ContentDigest, packet.ContentDigest, "packet property/evidence/rule order has no semantic effect");
        var reorderedOutput = Fixture.Output(); Reverse(reorderedOutput["proposals"]!.AsArray());
        foreach (var row in Fixture.Objects(reorderedOutput).Where(row => row.ContainsKey("text")))
        { Reverse(row["evidenceIds"]!.AsArray()); Reverse(row["ruleIds"]!.AsArray()); }
        foreach (var row in reorderedOutput["proposals"]!.AsArray()) Reverse(row!["conflictingEvidenceIds"]!.AsArray());
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Check.Equal(Proposal(packet, ReverseProperties(reorderedOutput).ToJsonString()).ContentDigest, snapshot.ContentDigest, "ordinal/culture-independent proposed property/set ordering");
        }
        finally { CultureInfo.CurrentCulture = culture; }
        foreach (var name in new[] { "baselineDigest", "profileDigest" })
            Check.That(Packet(Fixture.Change(Fixture.Input(), n => n["source"]![name] = new string('3', 64))).ContentDigest != packet.ContentDigest, "each frozen source digest bound");
        var otherRun = Packet(Fixture.Change(Fixture.Input(), n => n["source"]!["runId"] = "11111111-2222-3333-4444-666666666666"));
        Check.That(otherRun.ContentDigest != packet.ContentDigest, "different source run changes packet identity");
        DenyProposal(otherRun, Fixture.Read("provider-output.json"));
        var changedPacket = Packet(Fixture.Change(Fixture.Input(), n => n["evidence"]![0]!["configurationValue"] = "linear-4"));
        Check.That(changedPacket.ContentDigest != packet.ContentDigest, "changed minimized configuration changes packet identity");
        DenyProposal(changedPacket, Fixture.Read("provider-output.json"));
        DenyProposal(packet, Fixture.Change(Fixture.Output(), n => n["runId"] = "11111111-2222-3333-4444-666666666666"));
        DenyProposal(packet, Fixture.Change(Fixture.Output(), n => n["packetDigest"] = new string('0', 64)));
        var limitedEvidence = Packet(Fixture.Change(Fixture.Input(), n => n["evidence"]!.AsArray().RemoveAt(0)));
        DenyProposal(limitedEvidence, Fixture.Change(Fixture.Output(), n => n["packetDigest"] = limitedEvidence.ContentDigest));
        var limitedRules = Packet(Fixture.Change(Fixture.Input(), n => n["ruleIds"]!.AsArray().RemoveAt(1)));
        DenyProposal(limitedRules, Fixture.Change(Fixture.Output(), n => n["packetDigest"] = limitedRules.ContentDigest));
        Check.That(Proposal(packet, Fixture.Change(Fixture.Output(), n => Reverse(n["proposals"]![1]!["missingContext"]!.AsArray()))).ContentDigest != snapshot.ContentDigest, "missing context retains semantic array order");
        var twoFacts = Fixture.Change(Fixture.Output(), n =>
        {
            var fact = n["proposals"]![1]!["facts"]![0]!.DeepClone(); fact["text"] = "Second separate fictional observation.";
            n["proposals"]![1]!["facts"]!.AsArray().Add(fact);
        });
        var reversedFacts = JsonNode.Parse(twoFacts)!.AsObject(); Reverse(reversedFacts["proposals"]![1]!["facts"]!.AsArray());
        Check.That(Proposal(packet, twoFacts).ContentDigest != Proposal(packet, Fixture.Serialize(reversedFacts)).ContentDigest, "statement order retained rather than set-sorted");
        var empty = Proposal(packet, Fixture.Change(Fixture.Output(), n => n["proposals"] = new JsonArray()));
        Check.Equal(empty.ProposalCount, 0, "zero output is explicit empty proposed result");
        using (var e = JsonDocument.Parse(empty.CanonicalJson))
        { Check.Equal(e.RootElement.GetProperty("status").GetString(), "Proposed", "empty result cannot establish healthy evidence or approval"); Check.That(!e.RootElement.TryGetProperty("health", out _), "empty result has no health assertion"); }
        Check.That(typeof(SyntheticAiPacket).GetConstructors().Length == 0 && typeof(SyntheticAiPacket).GetProperties().All(p => p.SetMethod is null), "external packet construction and property mutation unavailable");
        Check.That(typeof(SyntheticAiProposalSnapshot).GetConstructors().Length == 0 && typeof(SyntheticAiProposalSnapshot).GetProperties().All(p => p.SetMethod is null), "external proposed snapshot construction and property mutation unavailable");
        Check.That(typeof(SyntheticAiPacket).Assembly.GetReferencedAssemblies().All(a => a.Name is not null && !a.Name.StartsWith("System.Net", StringComparison.Ordinal) && !a.Name.StartsWith("OpenAI", StringComparison.Ordinal) && !a.Name.StartsWith("Azure", StringComparison.Ordinal)), "no provider/network/SDK assembly reference");
        Check.Group("V8-001 composed fixed-provider independent canonical/source/conflict/empty/immutable/proposed-only oracles");
    }
    private static JsonNode ReverseProperties(JsonNode source)
    {
        if (source is JsonObject obj)
        {
            var result = new JsonObject(); foreach (var pair in obj.Reverse()) result[pair.Key] = pair.Value is null ? null : ReverseProperties(pair.Value); return result;
        }
        if (source is JsonArray array) return new JsonArray(array.Select(item => item is null ? null : ReverseProperties(item)).ToArray());
        return source.DeepClone();
    }
    private static void Reverse(JsonArray array)
    {
        var values = array.Select(item => item!.DeepClone()).Reverse().ToArray(); array.Clear(); foreach (var value in values) array.Add(value);
    }
    private static void ClosedObjects(JsonObject fixture, Action<string?> deny)
    {
        var objects = Fixture.Objects(fixture).ToArray();
        for (var index = 0; index < objects.Length; index++)
        {
            var i = index;
            foreach (var name in objects[index].Select(p => p.Key).ToArray())
            {
                deny(Fixture.Change(fixture, n => Fixture.Objects(n).ToArray()[i].Remove(name)));
                deny(Fixture.Change(fixture, n => Fixture.Objects(n).ToArray()[i][name] = null));
                deny(Fixture.Change(fixture, n => Fixture.Objects(n).ToArray()[i][name] = new JsonObject()));
            }
            foreach (var name in new[] { "rawEvidence", "tools", "fetch", "state", "approval", "severity", "policy", "storageLocator", "secret", "execute" })
                deny(Fixture.Change(fixture, n => Fixture.Objects(n).ToArray()[i][name] = "disabled fixture value"));
            var original = Fixture.Serialize(fixture); var target = Fixture.Serialize(objects[index]); var field = objects[index].First();
            var duplicate = target.Insert(target.Length - 1, "," + JsonSerializer.Serialize(field.Key) + ":" + field.Value!.ToJsonString());
            deny(original.Replace(target, duplicate, StringComparison.Ordinal));
            var encodedName = "\\u" + ((int)field.Key[0]).ToString("X4", CultureInfo.InvariantCulture) + field.Key[1..];
            duplicate = target.Insert(target.Length - 1, ",\"" + encodedName + "\":" + field.Value!.ToJsonString());
            deny(original.Replace(target, duplicate, StringComparison.Ordinal));
        }
    }
    private static void PacketDenials()
    {
        ClosedObjects(Fixture.Input(), DenyPacket);
        foreach (var malformed in new string?[] { null, "", " ", "null", "[]", "true", "0", "{", "{\"schemaVersion\":1,}", Fixture.Read("packet-input.json") + "{}", "//comment\n" + Fixture.Read("packet-input.json") }) DenyPacket(malformed);
        foreach (var field in new[] { "customerId", "projectId", "environmentId", "normalizationVersion", "redactionVersion", "promptVersion" })
            DenyPacket(Fixture.Change(Fixture.Input(), n => n["source"]![field] = "foreign-or-unsupported"));
        foreach (var id in new[] { "", "00000000-0000-0000-0000-000000000000", "11111111-2222-3333-4444-AAAAAAAAAAAA", "{11111111-2222-3333-4444-555555555555}", "11111111222233334444555555555555" }) DenyPacket(Fixture.Change(Fixture.Input(), n => n["source"]!["runId"] = id));
        foreach (var digest in new[] { "bad", new string('A', 64), new string('g', 64), new string('a', 63), new string('a', 65) })
            foreach (var field in new[] { "baselineDigest", "profileDigest" }) DenyPacket(Fixture.Change(Fixture.Input(), n => n["source"]![field] = digest));
        DenyPacket(Fixture.Change(Fixture.Input(), n => n["schemaVersion"] = "synthetic-ai-fixture-packet-v1"));
        foreach (var field in new[] { "configurationKey", "classification", "evidenceId" }) DenyPacket(Fixture.Change(Fixture.Input(), n => n["evidence"]![0]![field] = "foreign-or-unsupported"));
        foreach (var count in new[] { -1, 1000001 }) DenyPacket(Fixture.Change(Fixture.Input(), n => n["evidence"]![0]!["redactionCount"] = count));
        foreach (var text in new[] { "0.5", "1e999", "NaN", "\"1\"", "true" }) DenyPacket(Fixture.Serialize(Fixture.Input()).Replace("\"redactionCount\":2", "\"redactionCount\":" + text, StringComparison.Ordinal));
        DenyPacket(Fixture.Change(Fixture.Input(), n => n["evidence"]!.AsArray().Add(n["evidence"]![0]!.DeepClone())));
        DenyPacket(Fixture.Change(Fixture.Input(), n => n["ruleIds"]!.AsArray().Add(n["ruleIds"]![0]!.DeepClone())));
        DenyPacket(Fixture.Change(Fixture.Input(), n => n["ruleIds"]![0] = "fixture-rule-foreign-v1"));
        DenyPacket(Fixture.Change(Fixture.Input(), n => n["evidence"] = new JsonArray()));
        DenyPacket(Fixture.Change(Fixture.Input(), n => n["ruleIds"] = new JsonArray()));
        Check.Group("V8-002 independent packet closed shape/classification/allowlist/source/duplicate/malformed denial matrix");
    }
    private static void ProposalDenials()
    {
        var packet = Packet(Fixture.Read("packet-input.json"));
        ClosedObjects(Fixture.Output(), output => DenyProposal(packet, output));
        DenyProposal(null, Fixture.Read("provider-output.json"));
        foreach (var malformed in new string?[] { null, "", "null", "[]", "true", "0", "{", Fixture.Read("provider-output.json") + "{}", "/*comment*/" + Fixture.Read("provider-output.json") }) DenyProposal(packet, malformed);
        foreach (var field in new[] { "schemaVersion", "runId", "packetDigest" }) DenyProposal(packet, Fixture.Change(Fixture.Output(), n => n[field] = "foreign-or-unsupported"));
        DenyProposal(packet, Fixture.Change(Fixture.Output(), n => n["proposals"]!.AsArray().Add(n["proposals"]![0]!.DeepClone())));
        foreach (var id in new[] { "proposal-1", "proposal-001", "proposal-AA", "proposal-02 ", "unknown" }) DenyProposal(packet, Fixture.Change(Fixture.Output(), n => n["proposals"]![0]!["proposalId"] = id));
        foreach (var kind in new[] { "facts", "inferences", "assumptions", "suggestions" })
        {
            foreach (var field in new[] { "evidenceIds", "ruleIds" })
            {
                DenyProposal(packet, Fixture.Change(Fixture.Output(), n => n["proposals"]![1]![kind]![0]![field] = new JsonArray()));
                DenyProposal(packet, Fixture.Change(Fixture.Output(), n => n["proposals"]![1]![kind]![0]![field]![0] = "foreign"));
                DenyProposal(packet, Fixture.Change(Fixture.Output(), n => n["proposals"]![1]![kind]![0]![field]!.AsArray().Add(n["proposals"]![1]![kind]![0]![field]![0]!.DeepClone())));
            }
            DenyProposal(packet, Fixture.Change(Fixture.Output(), n => n["proposals"]![1]![kind]![0]!["text"] = "   "));
        }
        DenyProposal(packet, Fixture.Change(Fixture.Output(), n => { foreach (var kind in new[] { "facts", "inferences", "assumptions", "suggestions" }) n["proposals"]![0]![kind] = new JsonArray(); }));
        DenyProposal(packet, Fixture.Change(Fixture.Output(), n => n["proposals"]![1]!["uncertainty"] = ""));
        DenyProposal(packet, Fixture.Change(Fixture.Output(), n => n["proposals"]![1]!["missingContext"] = new JsonArray()));
        DenyProposal(packet, Fixture.Change(Fixture.Output(), n => n["proposals"]![1]!["missingContext"]![0] = " "));
        DenyProposal(packet, Fixture.Change(Fixture.Output(), n => n["proposals"]![1]!["conflictingEvidenceIds"] = new JsonArray(Fixture.EvidenceA)));
        DenyProposal(packet, Fixture.Change(Fixture.Output(), n => n["proposals"]![1]!["conflictingEvidenceIds"]![0] = "ev-" + new string('c', 64)));
        DenyProposal(packet, Fixture.Change(Fixture.Output(), n => n["proposals"]![1]!["conflictingEvidenceIds"]![0] = Fixture.EvidenceA));
        Check.Group("V8-003 independent proposed schema/run/digest/member citations/typed statements/conflict/duplicate denial matrix");
    }
    private static void BoundsAndHostileData()
    {
        var input = Fixture.Input(); var packet = Packet(Fixture.Serialize(input));
        DenyPacket(new string(' ', 65537) + Fixture.Serialize(input));
        DenyPacket(Fixture.Change(input, n => n["evidence"]![0]!["configurationValue"] = new string('x', 4097)));
        DenyPacket(Fixture.Serialize(input).Replace("linear-3", "\\uD800", StringComparison.Ordinal));
        DenyPacket(Fixture.Serialize(input).Replace("linear-3", "\\uDC00", StringComparison.Ordinal));
        DenyPacket(Fixture.Serialize(input).Replace("linear-3", "\ud800", StringComparison.Ordinal));
        DenyPacket(new string('[', 17) + "0" + new string(']', 17));
        var max = Fixture.Change(input, n => n["evidence"]![0]!["configurationValue"] = new string('x', 4096));
        Check.That(Packet(max).ContentDigest != packet.ContentDigest, "4096-unit allowed boundary is data-bound");
        foreach (var count in new[] { 0, 1000000 }) Packet(Fixture.Change(input, n => n["evidence"]![0]!["redactionCount"] = count));
        foreach (var value in new[] { "", " ", "😀" }) Packet(Fixture.Change(input, n => n["evidence"]![0]!["configurationValue"] = value));
        var evidence = input["evidence"]![0]!.DeepClone().AsObject();
        var seventeen = Fixture.Change(input, n =>
        {
            var records = new JsonArray(); for (var index = 0; index < 17; index++) { var copy = evidence.DeepClone(); copy["evidenceId"] = "ev-" + index.ToString("x64", CultureInfo.InvariantCulture); records.Add(copy); }
            n["evidence"] = records;
        }); DenyPacket(seventeen);
        var sixteen = JsonNode.Parse(seventeen)!.AsObject(); sixteen["evidence"]!.AsArray().RemoveAt(16);
        Check.Equal(Packet(Fixture.Serialize(sixteen)).EvidenceIds.Length, 16, "sixteen-record allowed boundary accepted");
        var hostilePacket = Packet(Fixture.Change(input, n => n["evidence"]![0]!["configurationValue"] = Fixture.Hostile));
        using (var parsed = JsonDocument.Parse(hostilePacket.CanonicalJson)) Check.Equal(parsed.RootElement.GetProperty("evidence")[1].GetProperty("configurationValue").GetString(), Fixture.Hostile, "hostile instructions remain inert exact configuration data");
        var encodedPacket = Packet(Fixture.Change(input, n => n["evidence"]![0]!["configurationValue"] = "ZW5jb2RlZCBmaXh0dXJlIGluc3RydWN0aW9u"));
        Check.That(encodedPacket.ContentDigest != packet.ContentDigest, "encoded fictional text remains data, no instruction dispatch");
        var output = Fixture.Output();
        DenyProposal(packet, new string(' ', 262145) + Fixture.Serialize(output));
        DenyProposal(packet, Fixture.Change(output, n => n["proposals"]![0]!["facts"]![0]!["text"] = new string('x', 4097)));
        DenyProposal(packet, Fixture.Serialize(output).Replace("Fictional retry policy was supplied.", "\\uD800", StringComparison.Ordinal));
        DenyProposal(packet, new string('[', 17) + "0" + new string(']', 17));
        DenyProposal(packet, Fixture.Change(output, n => { var rows = new JsonArray(); for (var i = 0; i < 17; i++) { var p = output["proposals"]![0]!.DeepClone(); p["proposalId"] = "proposal-" + i.ToString("D2", CultureInfo.InvariantCulture); rows.Add(p); } n["proposals"] = rows; }));
        foreach (var field in new[] { "facts", "inferences", "assumptions", "suggestions" })
            DenyProposal(packet, Fixture.Change(output, n => { var row = output["proposals"]![1]![field]![0]!.DeepClone(); var rows = new JsonArray(); for (var i = 0; i < 17; i++) rows.Add(row.DeepClone()); n["proposals"]![1]![field] = rows; }));
        DenyProposal(packet, Fixture.Change(output, n => { var rows = new JsonArray(); for (var i = 0; i < 17; i++) rows.Add("Missing fixture context"); n["proposals"]![1]!["missingContext"] = rows; }));
        var hostileOutput = Fixture.Change(output, n =>
        {
            foreach (var p in n["proposals"]!.AsArray())
            {
                foreach (var kind in new[] { "facts", "inferences", "assumptions", "suggestions" }) foreach (var statement in p![kind]!.AsArray()) statement!["text"] = Fixture.Hostile;
                p!["uncertainty"] = Fixture.Hostile;
            }
        });
        var hostileSnapshot = Proposal(packet, new FakeProvider(hostileOutput).Respond());
        using (var parsed = JsonDocument.Parse(hostileSnapshot.CanonicalJson))
            foreach (var p in parsed.RootElement.GetProperty("proposals").EnumerateArray())
            {
                Check.Equal(p.GetProperty("uncertainty").GetString(), Fixture.Hostile, "hostile uncertainty exact inert text");
                foreach (var kind in new[] { "facts", "inferences", "assumptions", "suggestions" }) foreach (var statement in p.GetProperty(kind).EnumerateArray()) Check.Equal(statement.GetProperty("text").GetString(), Fixture.Hostile, "hostile statements exact inert text");
            }
        Check.Equal(packet.ContentDigest, Fixture.PacketHash, "hostile output never mutates original packet");
        Check.Equal(Proposal(packet, Fixture.Read("provider-output.json")).ContentDigest, Fixture.ProposalHash, "hostile/denied values do not mutate subsequent validation");
        Check.Group("V8-004 UTF8/string/depth/record/statement bounds, escaped surrogate denial and hostile inert data");
    }
}
