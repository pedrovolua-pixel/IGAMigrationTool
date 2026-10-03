using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using SyntheticMcp;

// Expected values are fixture bytes plus the pinned contract rules, never production projections.
internal static class MinimizationCrossProduct
{
    private const string Digest = "613f1bca6039cddee6d19aae6e3ae23ff92bbda8027fafa08dcb79d53128291d";
    private static readonly string[] Mandatory = ["itemId", "category"];
    private static readonly EvidenceCategory[] Categories = [EvidenceCategory.Summary, EvidenceCategory.Configuration, EvidenceCategory.Identity, EvidenceCategory.Operations];
    private static readonly Dictionary<ResourceKind, string[]> Optional = new()
    {
        [ResourceKind.PublishedStatus] = ["assessmentState", "approvalState", "limitations"],
        [ResourceKind.Coverage] = ["assessed", "gap", "unavailable", "label", "limitations"],
        [ResourceKind.Scores] = ["health", "quality", "maturity", "reason"],
        [ResourceKind.Findings] = ["title", "summary", "severity", "reviewState", "confidence", "mandatoryReview", "referenceIds"],
        [ResourceKind.Recommendations] = ["findingId", "summary", "options", "priority", "effort", "reviewLabel"],
        [ResourceKind.ProtectedReferences] = ["availability", "reason", "currentAvailability", "availabilityReason"]
    };
    private static readonly ReferenceOverlay[] Overlays = [new(Availability.Available, SafeReason.None), new(Availability.Unavailable, SafeReason.Expired), new(Availability.Redacted, SafeReason.Redacted)];
    private sealed record Descriptor(ResourceKind Kind, string Id, EvidenceCategory Category, string PayloadDigest);
    private sealed record ItemRead(Scope Scope, string Report, ResourceKind Kind, string Id);
    private sealed record ManifestRead(Scope Scope, string Assessment, string Report);

    internal static async Task RunAsync()
    {
        var oracle = new Oracle();
        var assertions = 0;
        var cases = 0;
        var mainCases = 0;
        var mandatoryCases = 0;
        var overlayCases = 0;
        var unknownCases = 0;
        void Check(bool value, string label)
        {
            assertions++;
            if (!value) throw new InvalidOperationException("FAIL cycle03 minimization " + label);
        }
        oracle.Verify(Check);
        foreach (var identity in new[] { IdentityKind.NamedUser, IdentityKind.Service })
        {
            var caller = Program.User with { Kind = identity, IdentityId = identity == IdentityKind.Service ? "syn-service-a" : "syn-user-a" };
            foreach (var pair in Optional)
            {
                for (var categoryMask = 0; categoryMask < 16; categoryMask++)
                {
                    var categories = Categories.Where((_, index) => (categoryMask & (1 << index)) != 0).ToImmutableHashSet();
                    for (var fieldMask = 0; fieldMask < (1 << pair.Value.Length); fieldMask++)
                    {
                        var fields = pair.Value.Where((_, index) => (fieldMask & (1 << index)) != 0).Concat(Mandatory).ToImmutableHashSet(StringComparer.Ordinal);
                        await Case(caller, pair.Key, categories, fields, Overlays[0], $"main {identity}/{pair.Key}/c{categoryMask}/f{fieldMask}");
                        mainCases++;
                        if (pair.Key == ResourceKind.ProtectedReferences)
                        {
                            foreach (var overlay in Overlays.Skip(1))
                            {
                                await Case(caller, pair.Key, categories, fields, overlay, $"overlay {identity}/c{categoryMask}/f{fieldMask}/{overlay.CurrentAvailability}");
                                overlayCases++;
                            }
                        }
                    }
                    // Full optional grants with neither mandatory label, itemId only, or category only.
                    for (var mandatoryMask = 0; mandatoryMask < 3; mandatoryMask++)
                    {
                        var fields = pair.Value.Concat(Mandatory.Where((_, index) => (mandatoryMask & (1 << index)) != 0)).ToImmutableHashSet(StringComparer.Ordinal);
                        await Case(caller, pair.Key, categories, fields, Overlays[0], $"mandatory {identity}/{pair.Key}/c{categoryMask}/m{mandatoryMask}");
                        mandatoryCases++;
                    }
                }
                var unknown = new Program.World();
                unknown.Policy.FieldsOverride = pair.Value.Concat(Mandatory).Append("PROTECTED-UNKNOWN-GRANT-FIELD").ToImmutableHashSet(StringComparer.Ordinal);
                var unknownSource = new ReadSpy(unknown.Source);
                unknown.Host = new(unknownSource, unknown.Policy, unknown.Audit, unknown.Clock);
                var result = await unknown.Host.ReadAsync(caller, Program.Request(pair.Key));
                Check(result.Outcome == McpOutcome.DependencyUnavailable && result.Envelope.IsDefaultOrEmpty && result.NextCursor is null, $"unknown {identity}/{pair.Key} typed denial");
                Check(unknownSource.Manifests.Count == 0 && unknownSource.Items.Count == 0, $"unknown {identity}/{pair.Key} deny before all source reads");
                Check(unknown.Audit.Events.Count == 1 && unknown.Audit.Events[0].ReturnedFields.IsEmpty && unknown.Audit.Events[0].RedactedFields.IsEmpty && unknown.Audit.Events[0].Scope is null, $"unknown {identity}/{pair.Key} one generic empty-field audit");
                Check(!unknown.Audit.Serialized().Contains("PROTECTED-UNKNOWN-GRANT-FIELD", StringComparison.Ordinal), $"unknown {identity}/{pair.Key} no raw field echo");
                unknownCases++; cases++;
            }
        }
        Check(mainCases == 8448 && mandatoryCases == 576 && overlayCases == 1024 && unknownCases == 12 && cases == 10060, "exact request partition counts");
        Console.WriteLine($"PASS independent cycle03 minimization: {cases} requests ({mainCases} optional/category/identity cross-product, {mandatoryCases} mandatory-label omission, {overlayCases} additional current-overlay, {unknownCases} unknown-field controls); {assertions} assertions; P1D-T03/05/11 synthetic fixture only");

        async Task Case(Caller caller, ResourceKind kind, ImmutableHashSet<EvidenceCategory> categories, ImmutableHashSet<string> fields, ReferenceOverlay overlay, string label)
        {
            var world = new Program.World();
            world.Policy.Categories = categories;
            world.Policy.FieldsOverride = fields;
            world.Policy.Overlay = overlay;
            var selected = oracle.Descriptors.Where(d => d.Kind == kind && categories.Contains(d.Category)).ToArray();
            var selectedIds = selected.Select(d => d.Id).ToHashSet(StringComparer.Ordinal);
            // Inaccessible/other-kind content cannot be successfully parsed even if accidentally fetched.
            foreach (var id in world.Source.Payloads.Keys.Where(id => !selectedIds.Contains(id)).ToArray()) world.Source.Payloads[id] = Encoding.UTF8.GetBytes("PROTECTED-DENIED-CONTENT");
            var source = new ReadSpy(world.Source);
            var policy = new PolicySpy(world.Policy);
            world.Host = new(source, policy, world.Audit, world.Clock);
            var result = await world.Host.ReadAsync(caller, Program.Request(kind));
            cases++;
            var expectedManifestReads = categories.IsEmpty ? Array.Empty<ManifestRead>() : [new ManifestRead(Program.GoldenScope, "syn-assessment-a", "syn-report-a")];
            Check(source.Manifests.SequenceEqual(expectedManifestReads), label + " exact manifest bindings/read count");
            Check(source.Items.SequenceEqual(selected.Select(d => new ItemRead(Program.GoldenScope, "syn-report-a", kind, d.Id))), label + " exact selected payload reads and no referent/denied reads");
            Check(policy.OverlayIds.SequenceEqual(kind == ResourceKind.ProtectedReferences ? selected.Select(d => d.Id) : []), label + " overlay only for authorized references");
            Check(world.Audit.Events.Count == 1 && world.Audit.Failures.Count == 0, label + " one completion/no operational fault");
            var audit = world.Audit.Events.Single();
            Check(audit.IdentityKind == caller.Kind && audit.IdentityId == caller.IdentityId && audit.ResourceKind == kind && audit.CorrelationId == caller.CorrelationId && audit.ElapsedMilliseconds >= 0, label + " trusted audit context");
            if (selected.Length == 0)
            {
                Check(result.Outcome == McpOutcome.Unavailable && result.Envelope.IsDefaultOrEmpty && result.NextCursor is null && audit.Outcome == McpOutcome.Unavailable, label + " zero-selected unavailable/no content");
                Check(audit.ReturnedFields.IsEmpty && audit.RedactedFields.IsEmpty, label + " zero-selected no schema/count clues");
                if (categories.IsEmpty) Check(audit.Scope is null, label + " no category authority/no audit scope");
                return;
            }
            Check(result.Outcome == McpOutcome.Success && !result.Envelope.IsDefaultOrEmpty && result.NextCursor is null && audit.Outcome == McpOutcome.Success && audit.Scope == Program.GoldenScope, label + " authorized success/one final page");
            var returned = new HashSet<string>(StringComparer.Ordinal);
            var redacted = new HashSet<string>(StringComparer.Ordinal);
            var items = new JsonArray();
            foreach (var descriptor in selected)
            {
                var row = oracle.ExpectedRow(descriptor, categories, fields, overlay, redacted);
                returned.UnionWith(row.Select(p => p.Key));
                redacted.UnionWith(Mandatory.Concat(Optional[kind]).Except(row.Select(p => p.Key), StringComparer.Ordinal));
                items.Add(row);
            }
            var envelope = new JsonObject
            {
                ["contractVersion"] = "synthetic-published-health-read-v1",
                ["resourceKind"] = kind.ToString(),
                ["manifestDigest"] = Digest,
                ["bindings"] = oracle.Bindings.DeepClone(),
                ["items"] = items,
                ["nextCursor"] = null
            };
            Check(result.Envelope.AsSpan().SequenceEqual(Canonical(envelope)), label + " exact canonical schema/typed values/links/digest/bindings/no unauthorized counts");
            Check(audit.ReturnedFields.SequenceEqual(returned.Order(StringComparer.Ordinal)), label + " exact returned schema-name union");
            Check(audit.RedactedFields.SequenceEqual(redacted.Order(StringComparer.Ordinal)), label + " exact redacted schema-name union including partial links");
            Check(!Encoding.UTF8.GetString(result.Envelope.AsSpan()).Contains("PROTECTED-DENIED-CONTENT", StringComparison.Ordinal) && !world.Audit.Serialized().Contains("PROTECTED-DENIED-CONTENT", StringComparison.Ordinal), label + " no poisoned denied-content echo");
        }
    }

    private sealed class Oracle
    {
        private readonly byte[] manifest = File.ReadAllBytes(Program.World.PathOf("manifest.json"));
        private readonly Dictionary<string, byte[]> payloads = [];
        internal readonly Descriptor[] Descriptors;
        internal readonly JsonObject Bindings = new();
        internal Oracle()
        {
            var root = JsonNode.Parse(manifest)!.AsObject();
            Descriptors = root["items"]!.AsArray().Select(d => new Descriptor(Enum.Parse<ResourceKind>(d!["kind"]!.GetValue<string>()), d["itemId"]!.GetValue<string>(), Enum.Parse<EvidenceCategory>(d["category"]!.GetValue<string>()), d["payloadDigest"]!.GetValue<string>())).ToArray();
            foreach (var entry in root.Where(p => p.Key is not "collections" and not "items")) Bindings[entry.Key] = entry.Value!.DeepClone();
            foreach (var descriptor in Descriptors) payloads.Add(descriptor.Id, File.ReadAllBytes(Program.World.PathOf(descriptor.Id + ".json")));
        }
        internal void Verify(Action<bool, string> check)
        {
            check(Hash(manifest) == Digest && Program.GoldenDigest == Digest, "independent pinned manifest digest/helper alignment");
            check(manifest.AsSpan().SequenceEqual(Canonical(JsonNode.Parse(manifest)!)), "original manifest canonical bytes");
            var golden = JsonNode.Parse(File.ReadAllBytes(Program.World.PathOf("golden-hashes.json")))!;
            check(golden["manifest.json"]!.GetValue<string>() == Digest && Descriptors.Length == 12, "original fixture manifest golden/descriptor count");
            foreach (var descriptor in Descriptors)
            {
                var bytes = payloads[descriptor.Id];
                check(Hash(bytes) == descriptor.PayloadDigest && Hash(bytes) == golden[descriptor.Id + ".json"]!.GetValue<string>(), "original payload digest " + descriptor.Id);
                var row = JsonNode.Parse(bytes)!.AsObject();
                check(row.Select(p => p.Key).ToHashSet(StringComparer.Ordinal).SetEquals(Mandatory.Concat(Optional[descriptor.Kind].Except(["currentAvailability", "availabilityReason"]))), "independent source schema " + descriptor.Id);
                check(row["itemId"]!.GetValue<string>() == descriptor.Id && row["category"]!.GetValue<string>() == descriptor.Category.ToString() && bytes.AsSpan().SequenceEqual(Canonical(row)), "original descriptor labels/canonical bytes " + descriptor.Id);
            }
        }
        internal JsonObject ExpectedRow(Descriptor descriptor, ImmutableHashSet<EvidenceCategory> categories, ImmutableHashSet<string> fields, ReferenceOverlay overlay, HashSet<string> redacted)
        {
            var payload = JsonNode.Parse(payloads[descriptor.Id])!.AsObject();
            var row = new JsonObject { ["itemId"] = payload["itemId"]!.DeepClone(), ["category"] = payload["category"]!.DeepClone() };
            foreach (var field in Optional[descriptor.Kind].Where(fields.Contains))
            {
                if (field == "currentAvailability") row[field] = overlay.CurrentAvailability.ToString();
                else if (field == "availabilityReason") row[field] = overlay.AvailabilityReason.ToString();
                else if (field == "referenceIds")
                {
                    var original = payload[field]!.AsArray().Select(v => v!.GetValue<string>()).ToArray();
                    var kept = original.Where(id => Descriptors.Any(d => d.Kind == ResourceKind.ProtectedReferences && d.Id == id && categories.Contains(d.Category))).ToArray();
                    row[field] = new JsonArray(kept.Select(v => JsonValue.Create(v)).ToArray());
                    if (kept.Length != original.Length) redacted.Add(field);
                }
                else if (field != "findingId" || Descriptors.Any(d => d.Kind == ResourceKind.Findings && d.Id == payload[field]!.GetValue<string>() && categories.Contains(d.Category))) row[field] = payload[field]!.DeepClone();
            }
            return row;
        }
    }

    private sealed class ReadSpy(Program.Source source) : IPublicationReader
    {
        internal readonly List<ManifestRead> Manifests = [];
        internal readonly List<ItemRead> Items = [];
        public ValueTask<ManifestSource?> ReadManifestAsync(Scope scope, string assessmentId, string reportVersionId, CancellationToken token)
        {
            Manifests.Add(new(scope, assessmentId, reportVersionId));
            return source.ReadManifestAsync(scope, assessmentId, reportVersionId, token);
        }
        public ValueTask<ImmutableArray<byte>?> ReadItemAsync(Scope scope, string reportVersionId, ResourceKind kind, string itemId, CancellationToken token)
        {
            Items.Add(new(scope, reportVersionId, kind, itemId));
            return source.ReadItemAsync(scope, reportVersionId, kind, itemId, token);
        }
    }
    private sealed class PolicySpy(Program.Policy policy) : IReadPolicy
    {
        internal readonly List<string> OverlayIds = [];
        public ReadGrant? GetGrant(Caller caller, ReadRequest request) => policy.GetGrant(caller, request);
        public ReferenceOverlay GetReferenceAvailability(ReadGrant grant, string itemId)
        {
            OverlayIds.Add(itemId);
            return policy.GetReferenceAvailability(grant, itemId);
        }
        public bool TryCommit(ReadGrant grant, Action commit) => policy.TryCommit(grant, commit);
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static byte[] Canonical(JsonNode node)
    {
        using var document = JsonDocument.Parse(node.ToJsonString());
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping })) Write(writer, document.RootElement);
        return stream.ToArray();
    }
    private static void Write(Utf8JsonWriter writer, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var field in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)) { writer.WritePropertyName(field.Name); Write(writer, field.Value); }
            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray(); foreach (var item in element.EnumerateArray()) Write(writer, item); writer.WriteEndArray();
        }
        else if (element.ValueKind == JsonValueKind.Number) writer.WriteRawValue(element.GetDecimal().ToString("0.############################", CultureInfo.InvariantCulture));
        else element.WriteTo(writer);
    }
}
