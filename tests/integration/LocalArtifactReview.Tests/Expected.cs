using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

// Explicit contract primitives authored before reading A13/B13 implementation.
// Expected identities and canonical bytes never use production canonical helpers.
internal static class Expected
{
    internal const string ContractDigest = "a0dca320bcf11dda2f03abc16387f75395caff48e9c917c6b58a2826eb5a8b0f";
    internal const long MaximumRevision = 9007199254740991;
    internal const string Profile = "synthetic-review-maturity-fix-review-equal-v1";
    internal const string Application = "synthetic-fix-review-app-v1";
    internal const string TemplateVersion = "fictional-fix-templates-v1";
    internal const string TemplateDigest = "a40f3ccb1128581f36de236dbca3353097f4034b6738bcd01a98275229bee669";
    internal const string Schema = "synthetic-fix-package-demo-v1";
    internal const string Disclaimer = "Fictional fix-package preview. Every artifact is unverified and review-only; these generic examples are not supported One Identity remediation. No execution or approval is authorized.";
    internal static readonly string[] Presets = ["synthetic-analysis-healthy-v1", "synthetic-analysis-gaps-v1", "synthetic-analysis-findings-v1", "synthetic-analysis-mixed-v1"];
    internal static readonly (string Id, string Kind, string Text)[] Templates =
    [
        ("fictional-config-v1", "Configuration", "{\n  \"fixtureOnly\": true,\n  \"reviewRequired\": true\n}"),
        ("fictional-script-v1", "Script", "# Fictional review-only example. No customer-system action.\nWrite-Output 'Fixture review required'"),
        ("fictional-sql-v1", "Sql", "-- Fictional review-only example. No customer database or object.\nSELECT 'Fixture review required' AS FixtureMessage;")
    ];
    internal static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    internal static string Canonical(JsonNode? value)
    {
        if (value is JsonObject obj) return "{" + string.Join(",", obj.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => JsonSerializer.Serialize(pair.Key) + ":" + Canonical(pair.Value))) + "}";
        if (value is JsonArray array) return "[" + string.Join(",", array.Select(Canonical)) + "]";
        if (value is null) return "null";
        var element = JsonSerializer.SerializeToElement(value);
        return element.ValueKind == JsonValueKind.Number ? element.GetDecimal().ToString("G29", CultureInfo.InvariantCulture) : value.ToJsonString();
    }
    internal static string InputDigest(string plan, string versions, string baseline, string profile) => Hash(string.Concat(new[] { "synthetic-run-input-lock-v1", plan, versions, baseline, profile }.Select(value => value.Length + ":" + value)));
    internal static string PackageId(string finding, string run, JsonNode scope) => Hash(Canonical(new JsonObject { ["findingId"] = finding, ["runId"] = run, ["scope"] = scope.DeepClone() }));
    internal static string ScopedOptionId(string finding, string option, string run, JsonNode scope) => Hash(Canonical(new JsonObject { ["findingId"] = finding, ["optionId"] = option, ["runId"] = run, ["scope"] = scope.DeepClone() }));
    internal static string ArtifactId(string package, string option, string template) => Hash(Canonical(new JsonObject { ["packageId"] = package, ["scopedOptionId"] = option, ["templateId"] = template, ["templateVersion"] = TemplateVersion }));
    internal static JsonObject Binding(JsonObject package)
    {
        var guidance = package["guidance"]!; var source = guidance["source"]!;
        return new JsonObject
        {
            ["scope"] = source["scope"]!.DeepClone(),
            ["runId"] = source["runId"]!.DeepClone(),
            ["runRevision"] = source["runRevision"]!.DeepClone(),
            ["runInputDigest"] = source["runInputDigest"]!.DeepClone(),
            ["baselineId"] = source["baselineId"]!.DeepClone(),
            ["profileId"] = source["profileId"]!.DeepClone(),
            ["applicationVersion"] = source["frozenVersions"]!["applicationVersion"]!.DeepClone(),
            ["contractDigest"] = ContractDigest,
            ["sourceDigest"] = Hash(Canonical(package)),
            ["guidanceDigest"] = guidance["contentDigest"]!.DeepClone(),
            ["findingReviewDigest"] = source["reviewSnapshotDigest"]!.DeepClone(),
            ["templateVersion"] = TemplateVersion,
            ["templateDigest"] = TemplateDigest,
            ["findingRevisions"] = new JsonArray(guidance["findings"]!.AsArray().Select(f => (JsonNode)new JsonObject
            { ["findingId"] = f!["findingId"]!.DeepClone(), ["revision"] = f["findingRevision"]!.DeepClone() }).ToArray())
        };
    }
    internal static JsonArray Artifacts(JsonObject package)
    {
        var categories = package["guidance"]!["findings"]!.AsArray().ToDictionary(f => f!["findingId"]!.GetValue<string>(), f => f!["categoryId"]!.GetValue<string>(), StringComparer.Ordinal);
        return new JsonArray(package["packages"]!.AsArray().SelectMany(p => p!["options"]!.AsArray().SelectMany(o => o!["artifacts"]!.AsArray().Select(a => new JsonObject
        {
            ["findingId"] = p["findingId"]!.DeepClone(),
            ["categoryId"] = categories[p["findingId"]!.GetValue<string>()],
            ["packageId"] = p["packageId"]!.DeepClone(),
            ["scopedOptionId"] = o["scopedOptionId"]!.DeepClone(),
            ["artifactId"] = a!["artifactId"]!.DeepClone(),
            ["templateId"] = a["templateId"]!.DeepClone(),
            ["kind"] = a["kind"]!.DeepClone(),
            ["artifactTextDigest"] = Hash(a["text"]!.GetValue<string>())
        }))).OrderBy(a => a["findingId"]!.GetValue<string>(), StringComparer.Ordinal).ThenBy(a => a["artifactId"]!.GetValue<string>(), StringComparer.Ordinal).Select(a => (JsonNode)a).ToArray());
    }
    internal static string CommandDigest(JsonObject binding, string artifact, string actor, JsonNode command) => Hash(Canonical(new JsonObject
    { ["schemaVersion"] = "synthetic-fix-review-command-v1", ["scope"] = binding["scope"]!.DeepClone(), ["runId"] = binding["runId"]!.DeepClone(), ["artifactId"] = artifact, ["actorId"] = actor, ["command"] = command.DeepClone() }));
    internal static string State(string? latestKind, string? attestedSource, string currentSource) => latestKind is null || latestKind == "WithdrawReview" ? "Unverified" : attestedSource == currentSource ? "ReviewedForPlanning" : "NeedsReview";
    internal static JsonArray TemplateNodes() => new(Templates.Select(template => (JsonNode)new JsonObject { ["templateId"] = template.Id, ["kind"] = template.Kind, ["text"] = template.Text }).ToArray());
    internal static JsonObject FixPayload(JsonObject suppliedGuidance)
    {
        var guidance = (JsonObject)suppliedGuidance.DeepClone();
        var source = guidance["source"]!.AsObject();
        var run = source["runId"]!.GetValue<string>(); var scope = source["scope"]!;
        var packages = new JsonArray();
        foreach (var finding in guidance["findings"]!.AsArray().OrderBy(f => f!["findingId"]!.GetValue<string>(), StringComparer.Ordinal))
        {
            var findingId = finding!["findingId"]!.GetValue<string>(); var package = PackageId(findingId, run, scope);
            var options = new JsonArray();
            foreach (var option in finding["options"]!.AsArray().OrderBy(o => o!["scopedOptionId"]!.GetValue<string>(), StringComparer.Ordinal))
            {
                var optionId = ScopedOptionId(findingId, option!["optionId"]!.GetValue<string>(), run, scope);
                Check.Equal(option["scopedOptionId"]!.GetValue<string>(), optionId, "independent-upstream-scoped-option-identity");
                var artifacts = new JsonArray(Templates.Select(template => (JsonNode)new JsonObject
                {
                    ["artifactId"] = ArtifactId(package, optionId, template.Id),
                    ["templateId"] = template.Id,
                    ["kind"] = template.Kind,
                    ["status"] = "Unverified",
                    ["text"] = template.Text
                }).ToArray());
                options.Add(new JsonObject { ["scopedOptionId"] = optionId, ["artifacts"] = artifacts });
            }
            packages.Add(new JsonObject { ["packageId"] = package, ["findingId"] = findingId, ["options"] = options });
        }
        var warnings = new JsonArray("Finding confirmation, rejection or deferral does not review artifacts or validate remediation.",
            "Package identity ordering is not priority or effort. Existing finding groups are preserved without root-cause merging.",
            "Source guidance is retained verbatim as historical input; its unavailable sections describe that upstream projection.");
        if (packages.Count == 0) warnings.Add("No findings were supplied; no fix packages or actions are available.");
        return new JsonObject
        {
            ["schemaVersion"] = "synthetic-fix-package-preview-v1",
            ["status"] = "Unverified",
            ["disclaimer"] = Disclaimer,
            ["guidance"] = guidance,
            ["templateVersion"] = TemplateVersion,
            ["templateDigest"] = TemplateDigest,
            ["templates"] = TemplateNodes(),
            ["packages"] = packages,
            ["warnings"] = warnings,
            ["unavailableSections"] = new JsonArray("Consultant artifact review, approval history and content invalidation are unavailable.",
                "Priority, effort, customer objectives, task conversion/workflows and CSV/export are unavailable.",
                "Customer-system execution, external connectors, validated recovery/remediation and report publication are unavailable.")
        };
    }
}

internal static class Check
{
    internal static int Count;
    internal static string Last = "initialization";
    internal static void That(bool value, string code) { Last = code; if (!value) throw new InvalidOperationException(code); Count++; }
    internal static void Equal<T>(T actual, T expected, string code) => That(EqualityComparer<T>.Default.Equals(actual, expected), code);
    internal static void Bytes(byte[] actual, byte[] expected, string code) => That(actual.AsSpan().SequenceEqual(expected), code);
    internal static void Group(string code) => Console.WriteLine("PASS " + code);
}
