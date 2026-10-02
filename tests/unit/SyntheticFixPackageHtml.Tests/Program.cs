using System.Collections.Immutable;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using RecommendationGuidance;
using SyntheticFixPackages;

var assertions = 0;
var accepted = 0;
var denials = 0;
var stage = "initialize";
try
{
    var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    var directory = AppContext.BaseDirectory;
    var input = JsonSerializer.Deserialize<GuidanceInput>(File.ReadAllText(Path.Combine(directory, "fixture-input.json")), options)!;
    var hashes = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "oracle-hashes.json")))!;
    var constructor = typeof(FixPackageSnapshot).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
    void Check(string name, bool condition)
    {
        stage = name;
        if (!condition) throw new InvalidOperationException(name);
        assertions++;
    }
    string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    FixPackageSnapshot Build(GuidanceInput value)
    {
        var guidance = RecommendationGuidanceBuilder.Build(value);
        Check("real guidance composition", guidance.Succeeded);
        var result = FixPackageBuilder.Build(guidance.Snapshot);
        Check("real package composition", result.Succeeded);
        return result.Snapshot!;
    }
    FixPackageHtmlSnapshot Accept(string name, FixPackageSnapshot value)
    {
        var result = FixPackageHtmlRenderer.Render(value);
        Check(name, result.Succeeded && result.Issue is null && result.Snapshot is not null);
        accepted++;
        Check(name + " exact UTF8 HTML digest", result.Snapshot!.ContentDigest == Hash(result.Snapshot.Html));
        Check(name + " package binding", result.Snapshot.PackageDigest == value.ContentDigest);
        return result.Snapshot;
    }
    void Reject(string name, FixPackageSnapshot? value, FixPackageRenderIssue issue = FixPackageRenderIssue.InvalidSnapshot)
    {
        var result = FixPackageHtmlRenderer.Render(value);
        Check(name, !result.Succeeded && result.Issue == issue && result.Snapshot is null);
        denials++;
    }
    FixPackageSnapshot Forge(FixPackageSnapshot original, string property, object? value, bool refreshCache = false)
    {
        var args = new object?[] { original.SchemaVersion, original.Status, original.Disclaimer, original.Guidance,
            original.TemplateVersion, original.TemplateDigest, original.Templates, original.Packages, original.Warnings,
            original.UnavailableSections, original.CanonicalJson, original.ContentDigest };
        var parameters = constructor.GetParameters();
        args[Array.FindIndex(parameters, p => p.Name!.Equals(property, StringComparison.OrdinalIgnoreCase))] = value;
        var snapshot = (FixPackageSnapshot)constructor.Invoke(args);
        if (!refreshCache) return snapshot;
        var canonical = Encoding.UTF8.GetString(FixPackageBuilder.CanonicalPayload(snapshot));
        args[^2] = canonical; args[^1] = Hash(canonical);
        return (FixPackageSnapshot)constructor.Invoke(args);
    }
    foreach (var name in new[] { "complete", "empty" })
    {
        var value = Build(name == "complete" ? input : input with { Findings = [] });
        var output = Accept(name + " independent literal fixture", value);
        Check(name + " full canonical bytes", value.CanonicalJson == File.ReadAllText(Path.Combine(directory, name + "-package.json")));
        Check(name + " full HTML bytes", output.Html == File.ReadAllText(Path.Combine(directory, name + ".html")));
        Check(name + " literal package digest", value.ContentDigest == hashes[name]!["package"]!.GetValue<string>());
        Check(name + " literal HTML digest", output.ContentDigest == hashes[name]!["html"]!.GetValue<string>());
        Check(name + " literal guidance digest", value.Guidance.ContentDigest == hashes[name]!["guidance"]!.GetValue<string>());
        Check(name + " deterministic repeat", Accept("repeat " + name, value).Html == output.Html);
    }
    var original = Build(input);
    var html = Accept("baseline", original).Html;
    Check("encoded literal nonASCII", html.Contains("Current caf&#xE9; &#x3A9; &#x4E2D;&#x6587; &#x1F600;", StringComparison.Ordinal));
    Check("encoded script", html.Contains("Original &lt;script&gt;never()&lt;/script&gt;", StringComparison.Ordinal));
    Check("encoded apostrophe", html.Contains("&#x27;Fixture review required&#x27;", StringComparison.Ordinal));
    Check("all source frozen JSON properties", html.Contains("<dt>desiredOutcomeVersion</dt><dd>Not supplied</dd>", StringComparison.Ordinal));
    Check("exact CSP", html.Contains("content=\"default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'\"", StringComparison.Ordinal));
    Check("fixed keyboard skip", html.Contains("href=\"#main\"", StringComparison.Ordinal) && html.Contains("id=\"main\" tabindex=\"-1\"", StringComparison.Ordinal));
    Check("mobile wrapping", html.Contains("white-space:pre-wrap;overflow-wrap:anywhere", StringComparison.Ordinal));
    Check("historical boundary label", html.Contains("<h2>Historical upstream guidance</h2>", StringComparison.Ordinal) && html.Contains("Historical upstream warnings and unavailable sections", StringComparison.Ordinal));
    foreach (var forbidden in new[] { "<script", "<form", "<iframe", "<img", "<svg", "<button", "<input", "<link", "<object", " onclick=", "href=\"https:", "href=\"javascript:" })
        Check("no active markup " + forbidden, !html.Contains(forbidden, StringComparison.Ordinal));
    foreach (var property in typeof(GuidanceFinding).GetProperties())
    {
        var label = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
        Check("all finding fields visible " + label, html.Contains("<dt>" + label + "</dt>", StringComparison.Ordinal));
    }
    foreach (var property in typeof(GuidanceSourceBinding).GetProperties())
    {
        var label = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
        Check("all source fields visible " + label, html.Contains("<dt>" + label + "</dt>", StringComparison.Ordinal));
    }
    foreach (var currentState in new[] { "Proposed", "Confirmed", "Rejected", "Deferred" })
    {
        var value = Build(input with { Findings = [input.Findings[0] with { CurrentState = currentState }] });
        var output = Accept("finding state " + currentState, value);
        Check("finding state retained " + currentState, output.Html.Contains("finding state: " + currentState, StringComparison.Ordinal));
        Check("artifact Unverified invariant " + currentState, value.Packages[0].Options[0].Artifacts.All(a => a.Status == "Unverified") && output.Html.Split("<dt>Artifact status</dt><dd>Unverified</dd>").Length == 4);
        Check("package IDs state invariant " + currentState, value.Packages[0].PackageId == original.Packages[0].PackageId);
        Check("artifact IDs state invariant " + currentState, value.Packages[0].Options[0].Artifacts.SequenceEqual(original.Packages[0].Options[0].Artifacts));
    }
    var other = input.Findings[0] with { FindingId = new string('9', 64), CategoryId = "OPERATIONS", Occurrences = [input.Findings[0].Occurrences[0] with { OccurrenceId = new string('a', 64), ModuleId = "SyntheticOperations" }], Options = [input.Findings[0].Options[0] with { OptionId = "option-b" }, input.Findings[0].Options[0]] };
    var multi = input with { Findings = [other, input.Findings[0]] };
    var ordered = Accept("multi group", Build(multi));
    var permuted = Accept("permuted groups and options", Build(multi with { Findings = [input.Findings[0], other with { Options = other.Options.Reverse().ToImmutableArray() }] }));
    Check("permuted byte invariance", ordered.Html == permuted.Html);
    Check("one package per original group", ordered.Html.Split("<summary>Package for finding ").Length == 3);
    foreach (var hostile in new[] { "</summary><script>globalThis.owned=1</script>", "\"><img src=x onerror=alert(1)>", "<svg onload=alert(1)>", "<form action='https://example.invalid'><input autofocus></form>", "<style>@import url(https://example.invalid)</style>", "javascript:alert(1)", "=HYPERLINK(\"https://example.invalid\")", "$(curl https://example.invalid); rm -rf /fixture-only", "quotes ' \" + ` & < >", "é Ω 中文 😀", "\u0000\r\n\t\u2028\u2029", new string('x', 16384) })
    {
        var finding = input.Findings[0] with
        {
            OriginalTitle = hostile,
            PresentationTitle = hostile,
            BusinessContext = hostile,
            RootCause = hostile,
            Occurrences = [input.Findings[0].Occurrences[0] with { ObjectId = hostile, EvidenceReference = hostile }],
            Options = [new(hostile, hostile, hostile, hostile, hostile)],
            ValidationGuidance = [hostile],
            GuidanceReferences = [hostile],
            Assumptions = [hostile],
            Limitations = [hostile]
        };
        var output = Accept("hostile all prose", Build(input with { Findings = [finding] }));
        Check("hostile text encoded", output.Html.Contains(HtmlEncoder.Default.Encode(hostile), StringComparison.Ordinal));
        Check("hostile no injected script", !output.Html.Contains("<script", StringComparison.Ordinal) && !output.Html.Contains("<img", StringComparison.Ordinal) && !output.Html.Contains("<form", StringComparison.Ordinal));
    }
    Reject("missing snapshot", null, FixPackageRenderIssue.MissingSnapshot);
    foreach (var pair in new Dictionary<string, object?>
    {
        ["schemaVersion"] = "other",
        ["status"] = "Reviewed",
        ["disclaimer"] = "Approved",
        ["templateVersion"] = "fictional-v2",
        ["templateDigest"] = new string('0', 64),
        ["templates"] = original.Templates.SetItem(0, original.Templates[0] with { Text = "<script>execute()</script>" }),
        ["packages"] = original.Packages.SetItem(0, original.Packages[0] with { PackageId = new string('0', 64) }),
        ["warnings"] = ImmutableArray.Create("Approved"),
        ["unavailableSections"] = ImmutableArray<string>.Empty,
        ["guidance"] = original.Guidance with { Status = "Approved" }
    })
    {
        Reject("actual field forged retaining valid cache " + pair.Key, Forge(original, pair.Key, pair.Value));
        Reject("actual field forged self-consistent cache " + pair.Key, Forge(original, pair.Key, pair.Value, true));
    }
    var p = original.Packages[0]; var o = p.Options[0]; var a = o.Artifacts[0];
    foreach (var artifact in new[] { a with { ArtifactId = new string('0', 64) }, a with { TemplateId = "wrong" }, a with { Kind = "Executable" }, a with { Status = "Reviewed" }, a with { Text = "Execute now" } })
        Reject("forged artifact metadata/text complete cache", Forge(original, "packages", ImmutableArray.Create(p with { Options = [o with { Artifacts = o.Artifacts.SetItem(0, artifact) }] }), true));
    Reject("forged option identity", Forge(original, "packages", ImmutableArray.Create(p with { Options = [o with { ScopedOptionId = new string('0', 64) }] }), true));
    Reject("forged package membership", Forge(original, "packages", ImmutableArray.Create(p with { FindingId = new string('0', 64) }), true));
    foreach (var cache in new[] { "", "{}", original.CanonicalJson + " ", original.CanonicalJson.Replace("Unverified", "Reviewed", StringComparison.Ordinal) })
        Reject("cached JSON mismatch", Forge(original, "canonicalJson", cache));
    foreach (var digest in new[] { "", new string('0', 64), original.ContentDigest.ToUpperInvariant(), null })
        Reject("cached digest mismatch", Forge(original, "contentDigest", digest));
    foreach (var pair in new Dictionary<string, object?> { ["guidance"] = null, ["packages"] = default(ImmutableArray<FixPackage>), ["warnings"] = default(ImmutableArray<string>), ["unavailableSections"] = default(ImmutableArray<string>), ["canonicalJson"] = null, ["templates"] = ImmutableArray.Create((FixTemplate)null!) })
        Reject("malformed immutable graph " + pair.Key, Forge(original, pair.Key, pair.Value));
    Reject("default template collection", Forge(original, "templates", default(ImmutableArray<FixTemplate>)));
    Reject("default package collection", Forge(original, "packages", default(ImmutableArray<FixPackage>)));
    Reject("null package record", Forge(original, "packages", ImmutableArray.Create((FixPackage)null!)));
    Reject("default artifacts", Forge(original, "packages", ImmutableArray.Create(p with { Options = [o with { Artifacts = default }] })));
    Reject("null artifact", Forge(original, "packages", ImmutableArray.Create(p with { Options = [o with { Artifacts = ImmutableArray.Create((FixArtifact)null!) }] })));
    Reject("forged guidance source lock", Forge(original, "guidance", original.Guidance with { Source = original.Guidance.Source with { RunInputDigest = new string('0', 64) } }));
    Reject("forged original finding", Forge(original, "guidance", original.Guidance with { Findings = original.Guidance.Findings.SetItem(0, original.Guidance.Findings[0] with { OriginalTitle = "forged" }) }));
    Reject("forged guidance option status", Forge(original, "guidance", original.Guidance with { Findings = original.Guidance.Findings.SetItem(0, original.Guidance.Findings[0] with { Options = [original.Guidance.Findings[0].Options[0] with { Status = "Reviewed" }] }) }));
    Reject("forged canonical output above bound", Forge(original, "templates", ImmutableArray.Create(original.Templates[0] with { Text = new string('x', 33 * 1024 * 1024) })), FixPackageRenderIssue.OutputTooLarge);
    Check("fixed bounds", FixPackageHtmlRenderer.MaximumHtmlBytes == 64 * 1024 * 1024);
    foreach (var type in new[] { typeof(FixPackageSnapshot), typeof(FixPackageHtmlSnapshot), typeof(FixPackageRenderResult) })
    {
        Check("nonpublic snapshot constructor", type.GetConstructors().Length == 0);
        foreach (var property in type.GetProperties()) Check("immutable snapshot property " + property.Name, property.SetMethod is null);
    }
    Check("detached record copy", (a with { Text = "local copy" }).Text != a.Text && Accept("original unchanged", original).Html == html);
    // A valid large graph exercises full production composition, without fixture-only render tweaks.
    var small = input.Findings[0] with { RuleId = "R", OriginalTitle = "O", PresentationTitle = "P", BusinessContext = "", RootCause = "R", ValidationGuidance = ["V"], GuidanceReferences = ["G"], Assumptions = [], Limitations = ["L"] };
    var manyOptions = Enumerable.Range(0, 24999).Select(i => new GuidanceOptionInput(i.ToString(), "T", "P", "R", "G")).ToImmutableArray();
    var stress = Build(input with { Findings = [small with { Options = manyOptions }] });
    var large = Accept("actual 99997 package/option/artifact records", stress);
    Check("real stress canonical below 32MiB", Encoding.UTF8.GetByteCount(stress.CanonicalJson) < 32 * 1024 * 1024);
    Check("real stress complete no truncation", large.Html.Split("<article>").Length == 74998 && large.Html.Split("<summary>Option ").Length == 25000);
    Console.WriteLine($"Actual stress canonical bytes: {Encoding.UTF8.GetByteCount(stress.CanonicalJson)}; HTML bytes: {Encoding.UTF8.GetByteCount(large.Html)}.");
    var excessiveGuide = RecommendationGuidanceBuilder.Build(input with { Findings = [small with { Options = manyOptions.Add(new("extra", "T", "P", "R", "G")) }] });
    Check("upstream accepts independently excessive package record case", excessiveGuide.Succeeded);
    Reject("package 100001 records above bound", Forge(original, "guidance", excessiveGuide.Snapshot), FixPackageRenderIssue.OutputTooLarge);
    // Private sink boundary only: this is not a claim that a valid current-layout snapshot reaches64MiB.
    var sinkType = typeof(FixPackageHtmlRenderer).GetNestedType("Html", BindingFlags.NonPublic)!;
    var sink = Activator.CreateInstance(sinkType, true)!;
    var append = sinkType.GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance)!;
    append.Invoke(sink, [new string('a', FixPackageHtmlRenderer.MaximumHtmlBytes - 2)]);
    append.Invoke(sink, ["é"]);
    Check("UTF8 sink exact 64MiB accepted", Encoding.UTF8.GetByteCount(sink.ToString()!) == FixPackageHtmlRenderer.MaximumHtmlBytes);
    try { append.Invoke(sink, ["x"]); Check("sink above bound rejected", false); }
    catch (TargetInvocationException exception) { Check("sink above bound typed internal guard", exception.InnerException?.GetType().Name == "HtmlLimitException"); }
    foreach (string key in Environment.GetEnvironmentVariables().Keys)
        Check("credential-free environment", !key.StartsWith("PG", StringComparison.OrdinalIgnoreCase) && !key.Contains("OPENAI", StringComparison.OrdinalIgnoreCase) && !key.Contains("API_KEY", StringComparison.OrdinalIgnoreCase) && !key.Contains("CONNECTIONSTRING", StringComparison.OrdinalIgnoreCase));
    File.WriteAllText(Path.Combine(directory, "executed-complete.html"), html, new UTF8Encoding(false));
    Console.WriteLine($"B11 tests PASS: {assertions} assertions, {accepted} accepted render calls, {denials} payload-free denials.");
    Console.WriteLine($"Independent complete HTML SHA256: {hashes["complete"]!["html"]!.GetValue<string>()}");
}
catch (Exception exception)
{
    Console.Error.WriteLine($"B11 tests FAIL at {stage}; {exception.GetType().Name}; {assertions} completed assertions.");
    return 1;
}
return 0;
