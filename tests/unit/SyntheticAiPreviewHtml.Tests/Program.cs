using System.Collections.Immutable;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SyntheticAiPreview;

var checks = 0;
var accepted = 0;
var denied = 0;
var current = "initialization";
try
{
    var directory = AppContext.BaseDirectory;
    var json = File.ReadAllText(Path.Combine(directory, "golden-preview.json"));
    var golden = File.ReadAllText(Path.Combine(directory, "golden-preview.html"));
    var expectedDigest = File.ReadAllText(Path.Combine(directory, "golden-preview.sha256"));
    var original = JsonNode.Parse(json)!.AsObject();
    var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    var constructor = typeof(SyntheticAiPreviewSnapshot).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single();
    void Check(string name, bool condition)
    {
        current = name;
        if (!condition) throw new InvalidOperationException();
        checks++;
    }
    string Digest(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    string Canonical(JsonNode node)
    {
        JsonNode Order(JsonNode value)
        {
            if (value is JsonObject objectValue)
            {
                var result = new JsonObject();
                foreach (var pair in objectValue.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                    result[pair.Key] = pair.Value is null ? null : Order(pair.Value);
                return result;
            }
            if (value is JsonArray array) return new JsonArray(array.Select(value => value is null ? null : Order(value)).ToArray());
            return value.DeepClone();
        }
        return Order(node).ToJsonString();
    }
    SyntheticAiPreviewSnapshot Create(JsonObject node, string? canonical = null, string? digest = null)
    {
        var value = canonical ?? Canonical(node);
        return (SyntheticAiPreviewSnapshot)constructor.Invoke(new object[] {
            value, digest ?? Digest(value), node["source"]!.Deserialize<PreviewSource>(options)!,
            node["packetDigest"]!.GetValue<string>(), node["proposalDigest"]!.GetValue<string>(),
            node["proposals"]!.Deserialize<ImmutableArray<PreviewProposal>>(options)
        });
    }
    PreviewHtmlSnapshot Accept(string name, SyntheticAiPreviewSnapshot snapshot)
    {
        var result = SyntheticAiPreviewRenderer.Render(snapshot);
        Check(name, result.Succeeded && result.Issue is null && result.Snapshot is not null);
        accepted++;
        Check(name + " immutable exact output digest", result.Snapshot!.ContentDigest == Digest(result.Snapshot.Html));
        Check(name + " canonical binding", result.Snapshot.PreviewDigest == snapshot.ContentDigest);
        return result.Snapshot;
    }
    void Reject(string name, SyntheticAiPreviewSnapshot? snapshot, PreviewRenderIssue expected = PreviewRenderIssue.InvalidSnapshot)
    {
        var result = SyntheticAiPreviewRenderer.Render(snapshot);
        Check(name, !result.Succeeded && result.Snapshot is null && result.Issue == expected);
        denied++;
    }
    var snapshot = Create(original, json);
    var html = Accept("independently authored golden", snapshot);
    Check("complete independent exact UTF-8 bytes", Encoding.UTF8.GetBytes(html.Html).SequenceEqual(Encoding.UTF8.GetBytes(golden)));
    Check("independent literal HTML digest", html.ContentDigest == expectedDigest);
    Check("canonical oracle parity", Canonical(original) == json);
    Check("deterministic repetition", Accept("repeat", snapshot).Html == golden);
    Check("no BOM or final newline", !html.Html.StartsWith('\uFEFF') && !html.Html.EndsWith('\n'));
    Check("one h1", html.Html.Split("<h1>", StringSplitOptions.None).Length == 2);
    Check("fixed CSP", html.Html.Contains("content=\"default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'\"", StringComparison.Ordinal));
    Check("fixed skip focus target", html.Html.Contains("href=\"#preview-content\"", StringComparison.Ordinal) && html.Html.Contains("id=\"preview-content\" tabindex=\"-1\"", StringComparison.Ordinal));
    Check("proposed untrusted status", html.Html.Contains(SyntheticAiPreviewSnapshot.FixedDisclaimer, StringComparison.Ordinal) && html.Html.Contains("Status: Proposed", StringComparison.Ordinal));
    foreach (var forbidden in new[] { "<script", "<iframe", "<img", "<svg", "<object", "<form", "<button", "<input", "<link", " src=\"", "href=\"javascript:", "href=\"https:" })
        Check("no active markup " + forbidden, !html.Html.Contains(forbidden, StringComparison.Ordinal));
    Check("Unicode text encoding", html.Html.Contains("Unicode caf&#233; 中文 &#128512;", StringComparison.Ordinal));
    Check("escaped script text", html.Html.Contains("&lt;script&gt;alert(&#39;x&#39; &amp; &quot;y&quot;)&lt;/script&gt;", StringComparison.Ordinal));
    foreach (var label in new[] { "Facts", "Inferences", "Assumptions", "Suggestions", "Missing context", "Uncertainty", "Conflicting evidence IDs" })
        Check("complete typed label " + label, html.Html.Contains("<h4>" + label + "</h4>", StringComparison.Ordinal));
    foreach (var label in new[] { "Customer", "Project", "Environment", "Run ID", "Baseline digest", "Profile digest", "Normalization version", "Redaction version", "Prompt version", "Packet digest", "Proposal digest", "Preview digest" })
        Check("all source fields " + label, html.Html.Contains("<dt>" + label + "</dt><dd>", StringComparison.Ordinal));
    Check("missing context supplied order", html.Html.IndexOf("First context", StringComparison.Ordinal) < html.Html.IndexOf("Second context", StringComparison.Ordinal));
    Check("statement supplied order", html.Html.IndexOf("&lt;script&gt;", StringComparison.Ordinal) < html.Html.IndexOf("Unicode caf", StringComparison.Ordinal));
    Check("encoded uncertainty retained", html.Html.Contains("Uncertain &lt;style&gt;body{display:none}&lt;/style&gt;", StringComparison.Ordinal));

    var emptyNode = original.DeepClone().AsObject();
    emptyNode["proposals"] = new JsonArray();
    var empty = Accept("empty proposed snapshot", Create(emptyNode));
    Check("empty exact coverage disclaimer", empty.Html.Contains("No proposals were returned. This does not establish healthy or complete assessment coverage.", StringComparison.Ordinal));
    Check("empty retains Proposed warning/source", empty.Html.Contains("Status: Proposed", StringComparison.Ordinal) && empty.Html.Contains(snapshot.Source.RunId.ToString("D"), StringComparison.Ordinal));
    Check("empty has no synthetic article", !empty.Html.Contains("<article>", StringComparison.Ordinal));

    Reject("missing snapshot", null, PreviewRenderIssue.MissingSnapshot);
    Reject("corrupt digest", Create(original, json, new string('0', 64)));
    foreach (var canonical in new[] { "", "{}", json + " ", json.Replace("Proposed", "Approved", StringComparison.Ordinal), json.Replace("synthetic-ai-preview-v1", "synthetic-ai-preview-v2", StringComparison.Ordinal), json.Replace("Fictional offline preview.", "Trusted live report.", StringComparison.Ordinal), json + new string(' ', 4 * 1024 * 1024) })
        Reject("canonical mismatch or guard", Create(original, canonical));
    foreach (var field in original["source"]!.AsObject().Select(pair => pair.Key))
    {
        var changed = original.DeepClone().AsObject();
        changed["source"]![field] = field == "runId" ? "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa" : "changed";
        Reject("stale source " + field, Create(changed, json));
        if (field != "runId") Reject("forged source " + field, Create(changed));
    }
    foreach (var field in new[] { "packetDigest", "proposalDigest" })
    {
        var changed = original.DeepClone().AsObject();
        changed[field] = new string('e', 64);
        Reject("stale digest metadata " + field, Create(changed, json));
        changed[field] = "wrong";
        Reject("malformed digest metadata " + field, Create(changed));
    }
    foreach (var category in new[] { "facts", "inferences", "assumptions", "suggestions" })
    {
        foreach (var field in new[] { "text", "evidenceIds", "ruleIds" })
        {
            var changed = original.DeepClone().AsObject();
            var statement = changed["proposals"]![0]![category]![0]!;
            if (field == "text") statement[field] = "changed";
            else statement[field]![0] = "changed";
            Reject("stale statement " + category + " " + field, Create(changed, json));
        }
    }
    foreach (var field in new[] { "proposalId", "uncertainty", "missingContext", "conflictingEvidenceIds" })
    {
        var changed = original.DeepClone().AsObject();
        if (field is "proposalId" or "uncertainty") changed["proposals"]![0]![field] = "changed";
        else changed["proposals"]![0]![field]![0] = "changed";
        Reject("stale proposal " + field, Create(changed, json));
    }
    SyntheticAiPreviewSnapshot Forge(PreviewSource? source, ImmutableArray<PreviewProposal> proposals, string? canonical = null) =>
        (SyntheticAiPreviewSnapshot)constructor.Invoke(new object?[] { canonical ?? json, Digest(canonical ?? json), source,
            snapshot.PacketDigest, snapshot.ProposalDigest, proposals });
    Reject("null typed source", Forge(null, snapshot.Proposals));
    Reject("default typed proposals", Forge(snapshot.Source, default));
    Reject("null typed proposal", Forge(snapshot.Source, ImmutableArray.Create((PreviewProposal)null!)));
    Reject("default typed statements", Forge(snapshot.Source, ImmutableArray.Create(snapshot.Proposals[0] with { Facts = default })));
    Reject("default typed context", Forge(snapshot.Source, ImmutableArray.Create(snapshot.Proposals[0] with { MissingContext = default })));
    Reject("default typed conflicts", Forge(snapshot.Source, ImmutableArray.Create(snapshot.Proposals[0] with { ConflictingEvidenceIds = default })));
    Reject("null typed uncertainty", Forge(snapshot.Source, ImmutableArray.Create(snapshot.Proposals[0] with { Uncertainty = null! })));
    Reject("null typed proposal ID", Forge(snapshot.Source, ImmutableArray.Create(snapshot.Proposals[0] with { ProposalId = null! })));
    Reject("null typed context member", Forge(snapshot.Source, ImmutableArray.Create(snapshot.Proposals[0] with { MissingContext = ImmutableArray.Create((string)null!) })));
    Reject("null typed conflict member", Forge(snapshot.Source, ImmutableArray.Create(snapshot.Proposals[0] with { ConflictingEvidenceIds = ImmutableArray.Create((string)null!) })));
    foreach (var statement in new[] { null!, snapshot.Proposals[0].Facts[0] with { Text = null! },
        snapshot.Proposals[0].Facts[0] with { EvidenceIds = default }, snapshot.Proposals[0].Facts[0] with { RuleIds = default },
        snapshot.Proposals[0].Facts[0] with { EvidenceIds = ImmutableArray.Create((string)null!) },
        snapshot.Proposals[0].Facts[0] with { RuleIds = ImmutableArray.Create((string)null!) } })
        Reject("invalid typed statement member", Forge(snapshot.Source, ImmutableArray.Create(snapshot.Proposals[0] with { Facts = ImmutableArray.Create<PreviewStatement>(statement) })));
    Reject("non-ASCII canonical UTF-8 guard", Create(original, json + new string('中', 1400000)));
    var uppercaseDigest = original.DeepClone().AsObject();
    uppercaseDigest["packetDigest"] = new string('A', 64);
    Reject("uppercase digest denied", Create(uppercaseDigest));
    var emptyRun = original.DeepClone().AsObject();
    emptyRun["source"]!["runId"] = Guid.Empty.ToString("D");
    Reject("empty run denied", Create(emptyRun));
    foreach (var hostile in new[] { "</p><script>globalThis.owned=1</script><p>", "\"><img src=x onerror=alert(1)>", "<svg onload=alert(1)>", "javascript:alert(1)", "<a href='data:text/html,active'>text</a>", "<style>@import url(https://example.invalid)</style>", "<form action='https://example.invalid'><input autofocus></form>", "&lt;script&gt; entity stays text", "quotes ' \" & < >", "\r\n\t", "é Ω 中文 😀", new string('x', 4096) })
    {
        var changed = original.DeepClone().AsObject();
        changed["proposals"]![0]!["facts"]![0]!["text"] = hostile;
        changed["proposals"]![0]!["missingContext"]![0] = hostile;
        changed["proposals"]![0]!["uncertainty"] = hostile;
        var value = Accept("hostile text serialized", Create(changed));
        // Static expected escapes are checked above by the independently authored byte oracle.
        Check("hostile no injected node", !value.Html.Contains("<script", StringComparison.Ordinal) && !value.Html.Contains("<svg", StringComparison.Ordinal) && !value.Html.Contains("<form", StringComparison.Ordinal));
    }
    var blank = original.DeepClone().AsObject();
    foreach (var field in new[] { "inferences", "assumptions", "suggestions", "missingContext", "conflictingEvidenceIds" }) blank["proposals"]![0]![field] = new JsonArray();
    blank["proposals"]![0]!["uncertainty"] = "";
    var minimal = Accept("optional categories explicitly empty", Create(blank));
    foreach (var message in new[] { "No statements were supplied in this category.", "No missing context was declared.", "No uncertainty was declared.", "No conflicting evidence was declared." })
        Check("explicit absence label " + message, minimal.Html.Contains(message, StringComparison.Ordinal));
    var many = original.DeepClone().AsObject();
    var proposals = new JsonArray();
    for (var index = 0; index < 16; index++)
    {
        var proposal = original["proposals"]![0]!.DeepClone();
        proposal["proposalId"] = "proposal-" + index.ToString("D2");
        proposals.Add(proposal);
    }
    many["proposals"] = proposals;
    var sixteen = Accept("all sixteen proposals", Create(many));
    Check("all proposal count", sixteen.Html.Split("<article>", StringSplitOptions.None).Length == 17);
    for (var index = 0; index < 16; index++) Check("proposal ID retained", sixteen.Html.Contains("<h3>Proposal proposal-" + index.ToString("D2") + "</h3>", StringComparison.Ordinal));
    foreach (var proposal in proposals)
        foreach (var field in new[] { "facts", "inferences", "assumptions", "suggestions" })
        {
            var statements = new JsonArray();
            for (var index = 0; index < 16; index++)
            {
                var statement = original["proposals"]![0]!["facts"]![0]!.DeepClone();
                statement["text"] = new string('x', 3800);
                statements.Add(statement);
            }
            proposal![field] = statements;
        }
    Check("size test canonical below guard", Encoding.UTF8.GetByteCount(Canonical(many)) < 4 * 1024 * 1024);
    Reject("encoded output exceeds guard without truncation", Create(many), PreviewRenderIssue.OutputTooLarge);
    Check("snapshot constructor not public", typeof(SyntheticAiPreviewSnapshot).GetConstructors().Length == 0);
    Check("HTML snapshot constructor not public", typeof(PreviewHtmlSnapshot).GetConstructors().Length == 0);
    foreach (var type in new[] { typeof(SyntheticAiPreviewSnapshot), typeof(PreviewHtmlSnapshot) })
        foreach (var property in type.GetProperties()) Check("immutable property", property.SetMethod is null);
    var changedChild = snapshot.Proposals[0].Facts[0] with { Text = "changed outside snapshot" };
    Check("record copy cannot mutate snapshot", changedChild.Text != snapshot.Proposals[0].Facts[0].Text && Accept("unchanged after record copy", snapshot).Html == golden);
    Console.WriteLine($"B9 synthetic HTML tests passed: {checks} assertions, {accepted} accepted render calls, {denied} payload-free denials.");
    Console.WriteLine($"Independent full HTML SHA256: {expectedDigest}");
}
catch (Exception exception)
{
    Console.Error.WriteLine($"B9 synthetic HTML tests failed at {current}; exception code {exception.GetType().Name}; {checks} checks completed.");
    return 1;
}
return 0;
