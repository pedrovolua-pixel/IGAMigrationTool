using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SyntheticMcp;

internal static class Program
{
    private const string Golden = "fe987e49d33965fdbf760587eba7c5763f6ba229e64b607d20f7f75e421ff457";
    private static int checks;
    private static readonly Scope Scope = new("syn-customer", "syn-project", "syn-environment");
    private static string fixtures = "";
    private static void Check(bool condition, string label) { checks++; if (!condition) throw new InvalidOperationException(label); }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static byte[] Bytes(JsonNode node) => PublicationCodec.CanonicalBytes(JsonSerializer.SerializeToElement(node));
    private static ReadGrant Grant(ResourceKind kind) => new(IdentityKind.NamedUser, "syn-user", 1, Scope, kind, "syn-assessment", "syn-report", Golden, true, true, true, true, false, true, Enum.GetValues<EvidenceCategory>().ToImmutableHashSet(), Projection.Fields(kind));
    private static byte[] Fixture(string name) => File.ReadAllBytes(Path.Combine(fixtures, name + ".json"));
    private static ManifestSource Source(byte[] bytes) => new(bytes.ToImmutableArray(), Hash(bytes));
    private static ValidatedManifest Manifest() => PublicationCodec.Validate(Source(Fixture("manifest")), Grant(ResourceKind.Scores))!;

    public static void Main()
    {
        var root = Directory.GetCurrentDirectory();
        while (!Directory.Exists(Path.Combine(root, "specs"))) root = Directory.GetParent(root)!.FullName;
        fixtures = Path.Combine(root, "tests/unit/SyntheticMcpPublication.Tests/fixtures");
        var source = Source(Fixture("manifest"));
        Check(source.Digest == Golden, "independent Python manifest golden pinned");
        var manifest = Manifest();
        Check(manifest is not null && manifest.Items.Length == 6, "six exact descriptors");
        foreach (var kind in Enum.GetValues<ResourceKind>())
        {
            var descriptor = manifest!.Items.Single(x => x.Kind == kind);
            var bytes = Fixture(kind.ToString());
            Check(Hash(bytes) == descriptor.PayloadDigest, "independent Python payload digest " + kind);
            var payload = PublicationCodec.ReadPayload(bytes.ToImmutableArray(), descriptor, manifest);
            Check(payload.HasValue, "exact source payload " + kind);
            var overlay = kind == ResourceKind.ProtectedReferences ? new ReferenceOverlay(Availability.Unavailable, SafeReason.Expired) : null;
            foreach (var identity in new[] { IdentityKind.NamedUser, IdentityKind.Service })
            {
                var grant = Grant(kind) with { IdentityKind = identity };
                var projected = Projection.Project(payload!.Value, descriptor, manifest, grant, overlay);
                Check(projected is not null, "six resources identity " + kind + identity);
                Check(projected!.ReturnedFields.ToHashSet().SetEquals(Projection.Fields(kind)), "exact schema field set " + kind);
                var envelope = Projection.Envelope(manifest, kind, [projected], null);
                using var document = JsonDocument.Parse(envelope.AsMemory());
                var value = document.RootElement;
                Check(value.EnumerateObject().Select(x => x.Name).ToHashSet().SetEquals(["contractVersion", "resourceKind", "manifestDigest", "bindings", "items", "nextCursor"]), "flat closed envelope");
                Check(value.GetProperty("manifestDigest").GetString() == Golden && value.GetProperty("bindings").GetProperty("assessmentState").GetString() == "CompletedWithGaps", "original digest and frozen status");
                var bindings = value.GetProperty("bindings");
                Check(bindings.EnumerateObject().Count() == 14 && bindings.GetProperty("applicationVersion").GetString() == "syn-application" && bindings.GetProperty("baselineVersion").GetString() == "syn-baseline" && bindings.GetProperty("catalogVersion").GetString() == "syn-catalog" && bindings.GetProperty("scoringProfileVersion").GetString() == "syn-scoring" && bindings.GetProperty("maturityProfileVersion").GetString() == "syn-maturity" && bindings.GetProperty("approvalState").GetString() == "SyntheticApproved", "all frozen provenance fields");
                Check(PublicationCodec.CanonicalBytes(value).AsSpan().SequenceEqual(envelope.AsSpan()), "response canonical bytes");
            }
            Check(PublicationCodec.ReadPayload(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes) + " ").ToImmutableArray(), descriptor, manifest) is null, "altered payload rejects original hash");
            Check(Projection.Project(payload!.Value, descriptor, manifest, Grant(kind) with { Fields = Projection.Fields(kind).Add("rawSql") }, overlay) is null, "unknown trusted schema names deny");
            Check(Projection.Project(payload.Value, descriptor, manifest, Grant(kind) with { Categories = ImmutableHashSet<EvidenceCategory>.Empty }, overlay) is null, "category denial");
            Check(Projection.Project(payload.Value, descriptor, manifest, Grant(kind) with { ManifestDigest = new string('a', 64) }, overlay) is null, "trusted digest must bind projection");
            var minimal = Projection.Project(payload.Value, descriptor, manifest, Grant(kind) with { Fields = ImmutableHashSet<string>.Empty }, overlay)!;
            Check(minimal.ReturnedFields.SequenceEqual(["category", "itemId"]) && minimal.RedactedFields.Length == Projection.Fields(kind).Count - 2, "required only minimization and schema audit");
        }
        ManifestDenials(source);
        PayloadDenials();
        FilteringAndExactness();
        CanonicalCases();
        Console.WriteLine($"PASS {checks} synthetic publication assertions (P1D-T01/02/05/11)");
    }

    private static void ManifestDenials(ManifestSource source)
    {
        foreach (var grant in new[] { Grant(ResourceKind.Scores) with { Scope = Scope with { CustomerId = "syn-other" } }, Grant(ResourceKind.Scores) with { AssessmentId = "syn-other" }, Grant(ResourceKind.Scores) with { ReportVersionId = "syn-other" }, Grant(ResourceKind.Scores) with { ManifestDigest = new string('b', 64) } }) Check(PublicationCodec.Validate(source, grant) is null, "trusted binding mismatch");
        Check(PublicationCodec.Validate(source with { Digest = new string('a', 64) }, Grant(ResourceKind.Scores)) is null, "source digest cannot be substituted");
        foreach (var mutation in new Action<JsonObject>[]
        {
            x => x["contractVersion"] = "unknown", x => x["fixtureKind"] = "ReportDrafts", x => x["assessmentState"] = "Scoring", x => x["approvalState"] = "Approved",
            x => x.Remove("applicationVersion"), x => x["extra"] = "secret", x => x["environmentId"] = "real-environment", x => x["collections"] = new JsonArray("Scores"),
            x => x["items"]!.AsArray().Add(x["items"]![0]!.DeepClone()), x => x["items"]![0]!["kind"] = "0", x => x["items"]![0]!["category"] = "99", x => x["items"]![0]!["payloadDigest"] = new string('A', 64),
            x => x["items"]![0]!["extra"] = true, x => x["items"]!.AsArray().RemoveAt(2), x => x["items"]!.AsArray().RemoveAt(0)
        })
        {
            var node = JsonNode.Parse(Fixture("manifest"))!.AsObject(); mutation(node); var bytes = Bytes(node); var altered = Source(bytes);
            Check(PublicationCodec.Validate(altered, Grant(ResourceKind.Scores) with { ManifestDigest = altered.Digest }) is null, "invalid closed manifest/schema");
            Check(PublicationCodec.Validate(altered, Grant(ResourceKind.Scores)) is null, "changed publication never self-authorizes");
        }
        foreach (var text in new[] { " " + Encoding.UTF8.GetString(Fixture("manifest")), Encoding.UTF8.GetString(Fixture("manifest")) + "\n", "\uFEFF" + Encoding.UTF8.GetString(Fixture("manifest")), "{\"items\":[],\"items\":[]}", "{}" })
        {
            var altered = Source(Encoding.UTF8.GetBytes(text)); Check(PublicationCodec.Validate(altered, Grant(ResourceKind.Scores) with { ManifestDigest = altered.Digest }) is null, "noncanonical duplicate missing reject");
        }
        var huge = Source(new byte[1048577]); Check(PublicationCodec.Validate(huge, Grant(ResourceKind.Scores) with { ManifestDigest = huge.Digest }) is null, "manifest size cap");
        var empty = JsonNode.Parse(Fixture("manifest"))!.AsObject();
        empty["items"] = new JsonArray(empty["items"]![0]!.DeepClone(), empty["items"]![2]!.DeepClone());
        var emptySource = Source(Bytes(empty));
        Check(PublicationCodec.Validate(emptySource, Grant(ResourceKind.Scores) with { ManifestDigest = emptySource.Digest })!.Items.Length == 2, "empty optional groups allowed");
        var many = JsonNode.Parse(Fixture("manifest"))!.AsObject();
        var array = new JsonArray();
        for (var index = 0; index < 10001; index++) array.Add(new JsonObject { ["kind"] = "Coverage", ["itemId"] = "syn-" + index.ToString("D5"), ["category"] = "Summary", ["payloadDigest"] = new string('a', 64) });
        many["items"] = array;
        var manySource = Source(Bytes(many));
        Check(PublicationCodec.Validate(manySource, Grant(ResourceKind.Scores) with { ManifestDigest = manySource.Digest }) is null, "descriptor count or source cap denied");
    }

    private static (ValidatedManifest Manifest, ItemDescriptor Descriptor, ImmutableArray<byte> Bytes) MutatePayload(ResourceKind kind, Action<JsonObject> mutation)
    {
        var node = JsonNode.Parse(Fixture(kind.ToString()))!.AsObject(); mutation(node); var bytes = Bytes(node);
        var manifest = Manifest(); var old = manifest.Items.Single(x => x.Kind == kind); var descriptor = old with { PayloadDigest = Hash(bytes) };
        return (manifest with { Items = manifest.Items.Replace(old, descriptor) }, descriptor, bytes.ToImmutableArray());
    }
    private static void BadPayload(ResourceKind kind, Action<JsonObject> mutation)
    {
        var test = MutatePayload(kind, mutation); Check(PublicationCodec.ReadPayload(test.Bytes, test.Descriptor, test.Manifest) is null, "invalid committed payload " + kind);
    }
    private static void PayloadDenials()
    {
        foreach (var kind in Enum.GetValues<ResourceKind>())
        {
            BadPayload(kind, x => x["itemId"] = "syn-foreign"); BadPayload(kind, x => x["category"] = kind == ResourceKind.Recommendations ? "Summary" : "Operations");
            BadPayload(kind, x => x["category"] = "invalid"); BadPayload(kind, x => x["unknown"] = "PROTECTED_SENTINEL");
        }
        foreach (var text in new[] { "PROTECTED_SENTINEL", "https://secret.example", "SELECT * FROM Protected", "Fictional finding. Fictional summary.", "Fictional finding.  Fictional finding.", "Fictional finding. ", new string('x', 4097), "" })
        { BadPayload(ResourceKind.Findings, x => x["title"] = text); BadPayload(ResourceKind.Recommendations, x => x["options"] = new JsonArray(text)); }
        BadPayload(ResourceKind.PublishedStatus, x => x["assessmentState"] = "Completed"); BadPayload(ResourceKind.PublishedStatus, x => x["limitations"] = new JsonArray("raw"));
        foreach (var count in new[] { -1, 100001 }) BadPayload(ResourceKind.Coverage, x => x["assessed"] = count);
        BadPayload(ResourceKind.Coverage, x => x["gap"] = 1.5m); BadPayload(ResourceKind.Coverage, x => x["limitations"] = null);
        foreach (var number in new[] { -0.01m, 100.01m }) BadPayload(ResourceKind.Scores, x => x["health"] = number);
        BadPayload(ResourceKind.Scores, x => x["health"] = null); BadPayload(ResourceKind.Scores, x => x["maturity"] = 0); BadPayload(ResourceKind.Scores, x => x["maturity"] = 6); BadPayload(ResourceKind.Scores, x => x["maturity"] = 2.5m);
        BadPayload(ResourceKind.Findings, x => x["severity"] = "high"); BadPayload(ResourceKind.Findings, x => x["reviewState"] = "Published"); BadPayload(ResourceKind.Findings, x => x["confidence"] = "100"); BadPayload(ResourceKind.Findings, x => x["mandatoryReview"] = "true"); BadPayload(ResourceKind.Findings, x => x["referenceIds"] = new JsonArray("syn-foreign")); BadPayload(ResourceKind.Findings, x => x["referenceIds"] = new JsonArray("syn-reference", "syn-reference"));
        BadPayload(ResourceKind.Recommendations, x => x["findingId"] = "syn-foreign"); BadPayload(ResourceKind.Recommendations, x => x["reviewLabel"] = "Approved");
        BadPayload(ResourceKind.ProtectedReferences, x => x["availability"] = "0"); BadPayload(ResourceKind.ProtectedReferences, x => x["reason"] = "None ");
        foreach (var text in new[] { "Fictional finding.", string.Join(' ', Enumerable.Repeat("Fictional finding.", 215)), "Fictional résumé. Fictional résumé." })
        {
            var good = MutatePayload(ResourceKind.Findings, x => x["title"] = text);
            Check(PublicationCodec.ReadPayload(good.Bytes, good.Descriptor, good.Manifest).HasValue, "closed repeated atom allowed length " + text.Length);
        }
        BadPayload(ResourceKind.Findings, x => x["title"] = string.Join(' ', Enumerable.Repeat("Fictional finding.", 216)));
    }

    private static void FilteringAndExactness()
    {
        var manifest = Manifest();
        foreach (var kind in new[] { ResourceKind.Findings, ResourceKind.Recommendations })
        {
            var descriptor = manifest.Items.Single(x => x.Kind == kind); var payload = PublicationCodec.ReadPayload(Fixture(kind.ToString()).ToImmutableArray(), descriptor, manifest)!.Value;
            var alteredCategory = kind == ResourceKind.Findings ? ResourceKind.ProtectedReferences : ResourceKind.Findings;
            var other = manifest.Items.Single(x => x.Kind == alteredCategory);
            var changed = manifest with { Items = manifest.Items.Replace(other, other with { Category = EvidenceCategory.Configuration }) };
            var grant = Grant(kind) with { Categories = ImmutableHashSet.Create(descriptor.Category) };
            var item = Projection.Project(payload, descriptor, changed, grant, null)!;
            Check(item is not null, "linkage validates without reading other category");
            if (kind == ResourceKind.Findings) Check(item!.Content.GetProperty("referenceIds").GetArrayLength() == 0 && item.RedactedFields.Contains("referenceIds"), "denied reference ID removed");
            else Check(!item!.Content.TryGetProperty("findingId", out _) && item.RedactedFields.Contains("findingId"), "denied finding linkage omitted");
            Check(Encoding.UTF8.GetString(Projection.Envelope(changed, kind, [item!], null).AsSpan()).Contains(Golden, StringComparison.Ordinal), "filtered output retains original manifest commitment");
        }
        foreach (var kind in new[] { ResourceKind.Scores, ResourceKind.Coverage })
        {
            var test = MutatePayload(kind, x => { if (kind == ResourceKind.Scores) { x["health"] = null; x["quality"] = null; x["maturity"] = null; x["reason"] = "Unavailable"; } else { x["assessed"] = 100000; x["gap"] = 0; x["unavailable"] = 0; } });
            Check(PublicationCodec.ReadPayload(test.Bytes, test.Descriptor, test.Manifest).HasValue, "null score and count bounds exactness");
        }
        var score = manifest.Items.Single(x => x.Kind == ResourceKind.Scores); var scores = PublicationCodec.ReadPayload(Fixture("Scores").ToImmutableArray(), score, manifest)!.Value;
        Check(scores.GetProperty("health").GetDecimal() == 83.125m && scores.GetProperty("quality").GetDecimal() == 79.5m && scores.GetProperty("maturity").GetInt32() == 3, "decimal/maturity no recomputation");
        var reference = manifest.Items.Single(x => x.Kind == ResourceKind.ProtectedReferences); var referencePayload = PublicationCodec.ReadPayload(Fixture("ProtectedReferences").ToImmutableArray(), reference, manifest)!.Value;
        var projected = Projection.Project(referencePayload, reference, manifest, Grant(reference.Kind), new(Availability.Redacted, SafeReason.Redacted))!;
        Check(projected.Content.GetProperty("availability").GetString() == "Available" && projected.Content.GetProperty("currentAvailability").GetString() == "Redacted" && projected.Content.GetProperty("reason").GetString() == "None", "overlay preserves historical fields");
        Check(Projection.Project(referencePayload, reference, manifest, Grant(reference.Kind), new((Availability)99, SafeReason.None)) is null, "undefined overlay denied");
        Check(Projection.Project(referencePayload, reference, manifest, Grant(reference.Kind), null) is null, "missing overlay denied");
        Check(Projection.Envelope(manifest, ResourceKind.Findings, [], null).Length > 0, "empty authorized collection");
        Check(Projection.Envelope(manifest, (ResourceKind)99, [], null).IsEmpty, "unknown envelope kind denied");
        Check(Projection.Envelope(manifest, ResourceKind.Findings, [], "locator").IsEmpty, "invalid cursor cannot serialize");
    }

    private static void CanonicalCases()
    {
        foreach (var (input, expected) in new[] { ("{\"z\":83.12500,\"a\":\"Fictional résumé.\"}", "{\"a\":\"Fictional résumé.\",\"z\":83.125}"), ("[1e2,-0.0,1.2300]", "[100,0,1.23]"), ("{\"a\":\"\\u2028\\u0001\\n\\\\\\\"\"}", "{\"a\":\"\u2028\\u0001\\n\\\\\\\"\"}") })
        {
            using var document = JsonDocument.Parse(input); Check(Encoding.UTF8.GetString(PublicationCodec.CanonicalBytes(document.RootElement)) == expected, "independent canonical expected bytes");
        }
        foreach (var id in new[] { "syn-x", "syn-" + new string('a', 64) }) Check(PublicationCodec.IsId(id), "valid ID edge");
        foreach (var id in new[] { "syn-", "syn-" + new string('a', 65), "Syn-x", "syn-résumé", "syn-/secret", "syn-x\n", "syn-A" }) Check(!PublicationCodec.IsId(id), "invalid ID grammar");
        using var duplicate = JsonDocument.Parse("{\"x\":1,\"x\":2}");
        try { PublicationCodec.CanonicalBytes(duplicate.RootElement); Check(false, "duplicate canonical must throw"); } catch (JsonException) { Check(true, "duplicate canonical rejects"); }
        var manifest = Manifest(); var descriptor = manifest.Items.Single(x => x.Kind == ResourceKind.Scores);
        var noncanonical = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Fixture("Scores")).Replace("83.125", "8.3125e1", StringComparison.Ordinal));
        var substituted = descriptor with { PayloadDigest = Hash(noncanonical) }; var changed = manifest with { Items = manifest.Items.Replace(descriptor, substituted) };
        Check(PublicationCodec.ReadPayload(noncanonical.ToImmutableArray(), substituted, changed) is null, "committed exponent denied canonical");
        var oversized = new byte[65537]; substituted = descriptor with { PayloadDigest = Hash(oversized) }; changed = manifest with { Items = manifest.Items.Replace(descriptor, substituted) };
        Check(PublicationCodec.ReadPayload(oversized.ToImmutableArray(), substituted, changed) is null, "payload byte cap");
        var raw = Encoding.UTF8.GetString(Fixture("Findings"));
        foreach (var malformed in new[] { raw.Replace("\"title\":\"Fictional finding.\"", "\"title\":\"\\ud800\"", StringComparison.Ordinal), raw.Replace("\"title\":\"Fictional finding.\"", "\"title\":\"Fictional finding.\",\"title\":\"Fictional finding.\"", StringComparison.Ordinal), raw.Replace("\"title\":\"Fictional finding.\"", "\"title\":\"Fictional finding.\",\"unexpected\":" + new string('[', 20) + "0" + new string(']', 20), StringComparison.Ordinal) })
        {
            CheckRawPayload(Encoding.UTF8.GetBytes(malformed), ResourceKind.Findings, "surrogate duplicate/depth source denial");
        }
        var invalidUtf8 = Fixture("Findings"); invalidUtf8[20] = 255;
        CheckRawPayload(invalidUtf8, ResourceKind.Findings, "invalid UTF8 source denial");
        var unicode = MutatePayload(ResourceKind.Findings, x => x["summary"] = "Fictional résumé. Fictional résumé.");
        Check(unicode.Bytes.Length > Encoding.UTF8.GetString(unicode.Bytes.AsSpan()).Length, "source Unicode byte counts independent of characters");
    }

    private static void CheckRawPayload(byte[] bytes, ResourceKind kind, string label)
    {
        var manifest = Manifest(); var old = manifest.Items.Single(x => x.Kind == kind); var descriptor = old with { PayloadDigest = Hash(bytes) };
        var changed = manifest with { Items = manifest.Items.Replace(old, descriptor) };
        Check(PublicationCodec.ReadPayload(bytes.ToImmutableArray(), descriptor, changed) is null, label);
    }
}
