using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using SyntheticMcp;

internal static class Program
{
    private static int checks;
    private static readonly string[] Kinds = ["PublishedStatus", "Coverage", "Scores", "Findings", "Recommendations", "ProtectedReferences"];
    internal const string GoldenDigest = "613f1bca6039cddee6d19aae6e3ae23ff92bbda8027fafa08dcb79d53128291d";
    internal static readonly Scope GoldenScope = new("syn-customer-a", "syn-project-a", "syn-environment-a");
    internal static readonly Caller User = new(IdentityKind.NamedUser, "syn-user-a", Guid.Parse("10000000-0000-0000-0000-000000000001"));
    internal static readonly Dictionary<ResourceKind, string[]> Schema = new()
    {
        [ResourceKind.PublishedStatus] = ["itemId", "category", "assessmentState", "approvalState", "limitations"],
        [ResourceKind.Coverage] = ["itemId", "category", "assessed", "gap", "unavailable", "label", "limitations"],
        [ResourceKind.Scores] = ["itemId", "category", "health", "quality", "maturity", "reason"],
        [ResourceKind.Findings] = ["itemId", "category", "title", "summary", "severity", "reviewState", "confidence", "mandatoryReview", "referenceIds"],
        [ResourceKind.Recommendations] = ["itemId", "category", "findingId", "summary", "options", "priority", "effort", "reviewLabel"],
        [ResourceKind.ProtectedReferences] = ["itemId", "category", "availability", "reason", "currentAvailability", "availabilityReason"]
    };
    private static async Task Main()
    {
        await T01(); await T02(); await T03(); await T04(); await T05(); await T06();
        await T07(); await T08(); await T09(); await T10(); await T11();
        Console.WriteLine($"PASS independent SyntheticMcp integration: {checks} assertions; P1D-T01–11 executable coverage; T12 coordinator source-bound checks");
    }
    internal static void Check(bool value, string name)
    {
        checks++;
        if (!value) throw new InvalidOperationException("FAIL " + name);
    }
    internal static byte[] Canonical(JsonNode node)
    {
        using JsonDocument document = JsonDocument.Parse(node.ToJsonString());
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping })) WriteCanonical(writer, document.RootElement);
        return stream.ToArray();
    }
    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var field in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)) { writer.WritePropertyName(field.Name); WriteCanonical(writer, field.Value); }
                writer.WriteEndObject(); break;
            case JsonValueKind.Array:
                writer.WriteStartArray(); foreach (var item in element.EnumerateArray()) WriteCanonical(writer, item); writer.WriteEndArray(); break;
            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetDecimal().ToString("0.############################", CultureInfo.InvariantCulture)); break;
            default: element.WriteTo(writer); break;
        }
    }
    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    internal static byte[] Request(ResourceKind kind = ResourceKind.Findings, int? size = null, string? cursor = null, string report = "syn-report-a")
    {
        var node = new JsonObject { ["contractVersion"] = "synthetic-published-health-read-v1", ["resourceKind"] = kind.ToString(), ["assessmentId"] = "syn-assessment-a", ["reportVersionId"] = report };
        if (size.HasValue) node["pageSize"] = size.Value;
        if (cursor is not null) node["cursor"] = cursor;
        return Canonical(node);
    }
    private static JsonElement Success(McpResult result, string label, string expectedDigest = GoldenDigest)
    {
        Check(result.Outcome == McpOutcome.Success && !result.Envelope.IsDefaultOrEmpty, label + " success");
        var root = JsonDocument.Parse(result.Envelope.AsMemory()).RootElement.Clone();
        Check(root.EnumerateObject().Select(p => p.Name).ToHashSet().SetEquals(["bindings", "contractVersion", "items", "manifestDigest", "nextCursor", "resourceKind"]), label + " envelope fields");
        Check(root.GetProperty("manifestDigest").GetString() == expectedDigest, label + " golden digest");
        return root;
    }
    private static void Denied(McpResult result, string label, McpOutcome? expected = null)
    {
        Check(result.Outcome != McpOutcome.Success && result.Envelope.IsDefaultOrEmpty && result.NextCursor is null, label + " no content");
        if (expected.HasValue) Check(result.Outcome == expected, label + " typed outcome");
    }
    private static async Task T01()
    {
        foreach (var identity in new[] { User, User with { Kind = IdentityKind.Service, IdentityId = "syn-service-a" } })
            foreach (var kind in Enum.GetValues<ResourceKind>())
            {
                var world = new World();
                var root = Success(await world.Host.ReadAsync(identity, Request(kind)), "T01 " + kind);
                foreach (var item in root.GetProperty("items").EnumerateArray())
                {
                    Check(item.EnumerateObject().Select(p => p.Name).ToHashSet().SetEquals(Schema[kind]), "T01 exact item schema " + kind);
                    if (kind == ResourceKind.Scores) Check(item.GetProperty("health").GetDecimal() == 87.25m && item.GetProperty("quality").GetDecimal() == 91.125m && item.GetProperty("maturity").GetInt32() == 3, "T01 exact decimals");
                    if (kind == ResourceKind.Findings) Check(item.GetProperty("mandatoryReview").GetBoolean() && item.GetProperty("confidence").GetString() == "Medium", "T01 explicit review labels");
                }
                Check(world.Audit.Events.Count == 1 && world.Audit.Events[0].Outcome == McpOutcome.Success, "T01 audited success");
                Check(world.Source.ManifestReads == 1 && world.Source.ItemReads == root.GetProperty("items").GetArrayLength(), "T01 selected source reads");
                Success(await world.Host.ReadAsync(identity, Request(kind)), "T01 repeated");
            }
    }
    private static async Task T02()
    {
        foreach (var field in new[] { "contractVersion", "fixtureKind", "customerId", "projectId", "environmentId", "assessmentId", "reportVersionId", "baselineVersion", "catalogVersion", "scoringProfileVersion", "maturityProfileVersion", "applicationVersion", "assessmentState", "approvalState" })
        {
            var world = new World();
            var manifest = JsonNode.Parse(world.Source.Manifest)!; manifest[field] = field == "assessmentState" ? "Scoring" : "syn-tampered";
            world.Source.Manifest = Canonical(manifest);
            Denied(await world.Host.ReadAsync(User, Request()), "T02 altered canonical binding " + field);
            Check(world.Source.ItemReads == 0, "T02 integrity precedes payload " + field);
        }
        foreach (var mutation in new Action<JsonNode>[] { n => n.AsObject().Remove("baselineVersion"), n => n["fixtureKind"] = "ReportDrafts", n => n["assessmentState"] = "Scoring", n => n["approvalState"] = "Pending", n => n["unknown"] = "PROTECTED-SENTINEL", n => n["items"]!.AsArray().Add(n["items"]![0]!.DeepClone()), n => n["collections"]![0] = "Scores" })
        {
            var world = new World(); var manifest = JsonNode.Parse(world.Source.Manifest)!; mutation(manifest); world.Source.Manifest = Canonical(manifest); world.Rebind();
            Denied(await world.Host.ReadAsync(User, Request()), "T02 invalid schema"); Check(world.Source.ItemReads == 0, "T02 metadata reject before payload");
        }
        foreach (var change in new Action<World>[] { w => w.Source.DeclaredDigest = new string('0', 64), w => w.Policy.Digest = new string('0', 64), w => w.Source.Manifest = Encoding.UTF8.GetBytes(" " + Encoding.UTF8.GetString(w.Source.Manifest)), w => w.Source.Payloads["syn-finding-a"] = Encoding.UTF8.GetBytes("{}") })
        {
            var world = new World(); change(world); Denied(await world.Host.ReadAsync(User, Request()), "T02 digest/noncanonical bytes");
        }
        var absent = new World(); absent.Source.Absent = true; Denied(await absent.Host.ReadAsync(User, Request()), "T02 absent source", McpOutcome.Unavailable); Check(absent.Source.ManifestReads == 1 && absent.Source.ItemReads == 0, "T02 no source fallback");
    }
    private static async Task T03()
    {
        foreach (var deny in new Action<Policy>[] { p => p.NullGrant = true, p => p.ActiveIdentity = false, p => p.ActiveAssignment = false, p => p.CustomerPolicy = false, p => p.ResourceAvailable = false, p => p.ReadBlocked = true, p => p.ActionAllowed = false })
        {
            var world = new World(); deny(world.Policy); Denied(await world.Host.ReadAsync(User, Request()), "T03 denied authority", McpOutcome.Unavailable); Check(world.Source.ManifestReads == 0 && world.Source.ItemReads == 0, "T03 deny before reads");
        }
        foreach (var kind in new[] { IdentityKind.Anonymous, IdentityKind.ShareLink, IdentityKind.Support, IdentityKind.Workload, (IdentityKind)999 })
        {
            var world = new World(); Denied(await world.Host.ReadAsync(User with { Kind = kind }, Request()), "T03 unsupported identity"); Check(world.Source.ManifestReads == 0, "T03 no inferred human role");
        }
        var categories = new World(); categories.Policy.Categories = ImmutableHashSet.Create(EvidenceCategory.Summary);
        var items = Success(await categories.Host.ReadAsync(User, Request()), "T03 category-filtered").GetProperty("items");
        Check(items.GetArrayLength() == 1 && categories.Source.ReadIds.SequenceEqual(["syn-finding-a"]), "T03 category denied before load");
        Check(items[0].GetProperty("referenceIds").GetArrayLength() == 0, "T03 protected referent filtered");
        var recs = Success(await categories.Host.ReadAsync(User, Request(ResourceKind.Recommendations)), "T03 recommendations").GetProperty("items");
        Check(recs[0].TryGetProperty("findingId", out _) && !recs[1].TryGetProperty("findingId", out _), "T03 forbidden finding linkage omitted");
        var retained = new World(); retained.Policy.ReadBlocked = false; Success(await retained.Host.ReadAsync(User, Request()), "T03 nonblocking hold");
        var fields = new World(); fields.Policy.FieldsOverride = ImmutableHashSet.Create("itemId", "category", "severity");
        foreach (var item in Success(await fields.Host.ReadAsync(User, Request()), "T03 field policy").GetProperty("items").EnumerateArray()) Check(item.EnumerateObject().Select(p => p.Name).ToHashSet().SetEquals(["itemId", "category", "severity"]), "T03 field minimization");
        var unsafeFields = new World(); unsafeFields.Policy.FieldsOverride = ImmutableHashSet.Create("PROTECTED-SENTINEL"); Denied(await unsafeFields.Host.ReadAsync(User, Request()), "T03 unsafe trusted fields", McpOutcome.DependencyUnavailable); Check(!unsafeFields.Audit.Serialized().Contains("PROTECTED-SENTINEL", StringComparison.Ordinal), "T03 unknown field audit no echo");
    }
    private static async Task T04()
    {
        var requests = JsonSerializer.Deserialize<string[]>(File.ReadAllText(World.PathOf("denial-corpus.json")))!;
        foreach (var request in requests)
        {
            var world = new World(); Denied(await world.Host.ReadAsync(User, Encoding.UTF8.GetBytes(request)), "T04 denial corpus", McpOutcome.InvalidRequest);
            Check(world.Source.ManifestReads == 0 && world.Source.ItemReads == 0, "T04 no source reads"); Check(!world.Audit.Serialized().Contains("PROTECTED-SENTINEL", StringComparison.Ordinal), "T04 no audit echo");
        }
        foreach (var bytes in new[] { new byte[] { 0xff, 0xfe }, new byte[16385] }) { var world = new World(); Denied(await world.Host.ReadAsync(User, bytes), "T04 bytes malformed", McpOutcome.InvalidRequest); }
    }
    private static async Task T05()
    {
        foreach (var (id, field, kind) in new[] { ("syn-finding-a", "title", ResourceKind.Findings), ("syn-finding-a", "summary", ResourceKind.Findings), ("syn-rec-a", "summary", ResourceKind.Recommendations), ("syn-rec-a", "priority", ResourceKind.Recommendations), ("syn-rec-a", "options", ResourceKind.Recommendations), ("syn-ref-a", "reason", ResourceKind.ProtectedReferences) })
        {
            var world = new World(); var node = JsonNode.Parse(world.Source.Payloads[id])!; node[field] = field == "options" ? new JsonArray("PROTECTED-SENTINEL") : JsonValue.Create("PROTECTED-SENTINEL"); world.UpdatePayload(id, node);
            Denied(await world.Host.ReadAsync(User, Request(kind)), "T05 protected nested payload"); Check(!world.Audit.Serialized().Contains("PROTECTED-SENTINEL", StringComparison.Ordinal), "T05 no sentinel audit");
        }
        var invisible = new World(); invisible.Policy.Categories = ImmutableHashSet.Create(EvidenceCategory.Summary); invisible.Source.Payloads["syn-finding-b"] = Encoding.UTF8.GetBytes("PROTECTED-SENTINEL");
        Success(await invisible.Host.ReadAsync(User, Request()), "T05 inaccessible content not loaded"); Check(!invisible.Source.ReadIds.Contains("syn-finding-b"), "T05 never fetch to redact");
    }
    private static async Task T06()
    {
        var world = new World(); string? cursor = null; var ids = new List<string>();
        do
        {
            var result = await world.Host.ReadAsync(User, Request(size: 1, cursor: cursor)); var root = Success(result, "T06 page");
            ids.AddRange(root.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("itemId").GetString()!)); cursor = result.NextCursor;
            if (cursor is not null) Check(cursor.Length == 64 && cursor.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'), "T06 opaque random handle");
        } while (cursor is not null);
        Check(ids.SequenceEqual(["syn-finding-a", "syn-finding-b", "syn-finding-c"]), "T06 ordinal stable pages");
        var firstWorld = new World(); var first = await firstWorld.Host.ReadAsync(User, Request(size: 1)); var handle = first.NextCursor!;
        foreach (var request in new[] { Request(size: 2, cursor: handle), Request(ResourceKind.Recommendations, 1, handle), Request(size: 1, cursor: new string('0', 64)), Request(size: 1, cursor: "../../secret"), Request(size: 1, cursor: handle, report: "syn-report-b") }) Denied(await firstWorld.Host.ReadAsync(User, request), "T06 cursor mismatch");
        Denied(await firstWorld.Host.ReadAsync(User with { IdentityId = "syn-user-b" }, Request(size: 1, cursor: handle)), "T06 foreign identity", McpOutcome.Unavailable);
        var restart = new World(); Denied(await restart.Host.ReadAsync(User, Request(size: 1, cursor: handle)), "T06 restart loses handle", McpOutcome.Unavailable);
        var empty = new World(); empty.RemoveKind(ResourceKind.Findings); Check(Success(await empty.Host.ReadAsync(User, Request()), "T06 empty", empty.Policy.Digest).GetProperty("items").GetArrayLength() == 0, "T06 empty collection");
        var max = new World(); Check(Success(await max.Host.ReadAsync(User, Request(size: 100)), "T06 max size").GetProperty("items").GetArrayLength() == 3, "T06 maximum page");
    }
    private static async Task T07()
    {
        foreach (var revoke in new Action<Policy>[] { p => p.ActiveAssignment = false, p => p.CustomerPolicy = false, p => p.ResourceAvailable = false, p => p.Categories = ImmutableHashSet<EvidenceCategory>.Empty })
        {
            var world = new World(); var first = await world.Host.ReadAsync(User, Request(size: 1)); world.Policy.Change(revoke);
            Denied(await world.Host.ReadAsync(User, Request(size: 1, cursor: first.NextCursor)), "T07 between pages revoked", McpOutcome.Unavailable);
            world.Policy.Change(p => { p.ActiveAssignment = p.CustomerPolicy = p.ResourceAvailable = true; p.Categories = Enum.GetValues<EvidenceCategory>().ToImmutableHashSet(); });
            Denied(await world.Host.ReadAsync(User, Request(size: 1, cursor: first.NextCursor)), "T07 revoke regrant stale handle", McpOutcome.Unavailable);
        }
        foreach (var revoke in new Action<Policy>[] { p => p.ActiveAssignment = false, p => p.ResourceAvailable = false, p => p.Overlay = new(Availability.Unavailable, SafeReason.Expired) })
        {
            var world = new World(); world.Source.Block = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var pending = world.Host.ReadAsync(User, Request()); await world.Source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2)); world.Policy.Change(revoke); world.Source.Block.SetResult();
            Denied(await pending, "T07 pre-emission revision race", McpOutcome.Unavailable); Check(world.Audit.Events.Count == 1 && world.Audit.Events[0].Scope is null, "T07 stale completion generic scope");
        }
        var expired = new World(); expired.Policy.Change(p => p.Overlay = new(Availability.Unavailable, SafeReason.Expired));
        var refs = Success(await expired.Host.ReadAsync(User, Request(ResourceKind.ProtectedReferences)), "T07 current overlay").GetProperty("items");
        Check(refs[0].GetProperty("availability").GetString() == "Available" && refs[0].GetProperty("currentAvailability").GetString() == "Unavailable", "T07 frozen vs current evidence availability");
    }
    private static async Task T08()
    {
        var world = new World(); for (var i = 0; i < 60; i++) Check((await world.Host.ReadAsync(User, Request())).Outcome == McpOutcome.Success, "T08 identity admitted " + i);
        Denied(await world.Host.ReadAsync(User, Request()), "T08 identity rate61", McpOutcome.Limited); world.Clock.Value = TimeSpan.FromSeconds(60); Check((await world.Host.ReadAsync(User, Request())).Outcome == McpOutcome.Success, "T08 rate exact window reset");
        var denied = new World(); denied.Policy.ActionAllowed = false; for (var i = 0; i < 60; i++) Check((await denied.Host.ReadAsync(User, Request())).Outcome == McpOutcome.Unavailable, "T08 admitted denials consume budget"); Denied(await denied.Host.ReadAsync(User, Request()), "T08 denied rate budget", McpOutcome.Limited);
        var customers = new World(); for (var i = 0; i < 300; i++) Check((await customers.Host.ReadAsync(User with { IdentityId = "syn-user-" + i / 60 }, Request())).Outcome == McpOutcome.Success, "T08 customer rate " + i); Denied(await customers.Host.ReadAsync(User with { IdentityId = "syn-next" }, Request()), "T08 customer rate301", McpOutcome.Limited);
        var concurrent = new World(); concurrent.Source.Block = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var pending = Enumerable.Range(0, 4).Select(_ => concurrent.Host.ReadAsync(User, Request())).ToArray(); await concurrent.Source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Denied(await concurrent.Host.ReadAsync(User, Request()), "T08 identity concurrency5", McpOutcome.Limited); concurrent.Source.Block.SetResult(); await Task.WhenAll(pending); Check((await concurrent.Host.ReadAsync(User, Request())).Outcome == McpOutcome.Success, "T08 concurrency leases released");
        var saturated = new World(); saturated.Source.Block = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var customerPending = Enumerable.Range(0, 16).Select(i => saturated.Host.ReadAsync(User with { IdentityId = "syn-concurrent-" + i }, Request())).ToArray();
        await saturated.Source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2)); Denied(await saturated.Host.ReadAsync(User with { IdentityId = "syn-concurrent-17" }, Request()), "T08 customer concurrency17", McpOutcome.Limited);
        saturated.AddCustomer("syn-isolated"); Check((await saturated.Host.ReadAsync(User with { IdentityId = "syn-isolated" }, Request())).Outcome == McpOutcome.Success, "T08 unrelated customer progresses under saturation");
        saturated.Source.Block.SetResult(); Check((await Task.WhenAll(customerPending)).All(r => r.Outcome == McpOutcome.Success), "T08 exact16 customer leases admitted");
        customers.AddCustomer("syn-next"); for (var i = 0; i < 60; i++) Check((await customers.Host.ReadAsync(User with { IdentityId = "syn-next" }, Request())).Outcome == McpOutcome.Success, "T08 failed paired customer quota did not consume identity " + i);
        var registry = new World(); for (var i = 0; i < 128; i++) { registry.Clock.Value = TimeSpan.FromSeconds(i / 50 * 60); Check((await registry.Host.ReadAsync(User, Request(size: 1))).Outcome == McpOutcome.Success, "T08 cursor identity capacity " + i); } Denied(await registry.Host.ReadAsync(User, Request(size: 1)), "T08 cursor identity capacity129", McpOutcome.Limited);
        var registryCustomer = new World(); for (var i = 0; i < 512; i++) { registryCustomer.Clock.Value = TimeSpan.FromSeconds(i / 200 * 60); Check((await registryCustomer.Host.ReadAsync(User with { IdentityId = "syn-cursor-" + i % 4 }, Request(size: 1))).Outcome == McpOutcome.Success, "T08 cursor customer capacity " + i); } Denied(await registryCustomer.Host.ReadAsync(User with { IdentityId = "syn-cursor-new" }, Request(size: 1)), "T08 cursor customer capacity513", McpOutcome.Limited);
        registryCustomer.AddCustomer("syn-cursor-isolated"); Check((await registryCustomer.Host.ReadAsync(User with { IdentityId = "syn-cursor-isolated" }, Request(size: 1))).Outcome == McpOutcome.Success, "T08 other customer cursor progress");
        var cancel = new World(); cancel.Source.Block = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); using var cts = new CancellationTokenSource(); var cancelled = cancel.Host.ReadAsync(User, Request(), cts.Token); await cancel.Source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2)); cts.Cancel(); Denied(await cancelled.WaitAsync(TimeSpan.FromSeconds(2)), "T08 cancellation", McpOutcome.Cancelled); cancel.Source.Block.SetResult(); Check((await cancel.Host.ReadAsync(User, Request())).Outcome == McpOutcome.Success, "T08 cancellation lease recovery");
        var failure = new World(); failure.Source.Throw = true; Denied(await failure.Host.ReadAsync(User, Request()), "T08 source throw", McpOutcome.DependencyUnavailable); failure.Source.Throw = false; Success(await failure.Host.ReadAsync(User, Request()), "T08 exception lease recovery");
    }
    private static async Task T09()
    {
        var world = new World(); var first = await world.Host.ReadAsync(User, Request(size: 1)); world.Clock.Value = TimeSpan.FromSeconds(299); Success(await world.Host.ReadAsync(User, Request(size: 1, cursor: first.NextCursor)), "T09 before expiry"); world.Clock.Value = TimeSpan.FromSeconds(300); Denied(await world.Host.ReadAsync(User, Request(size: 1, cursor: first.NextCursor)), "T09 exact expiry", McpOutcome.Unavailable);
        world.Clock.Value = TimeSpan.FromSeconds(0); Denied(await world.Host.ReadAsync(User, Request(size: 1, cursor: first.NextCursor)), "T09 rollback cannot restore", McpOutcome.Unavailable);
        var negative = new World(); negative.Clock.Value = TimeSpan.FromSeconds(-1); Denied(await negative.Host.ReadAsync(User, Request()), "T09 negative clock", McpOutcome.DependencyUnavailable);
        var badClock = new World(); badClock.Clock.Throw = true; Denied(await badClock.Host.ReadAsync(User, Request()), "T09 clock throw", McpOutcome.DependencyUnavailable);
        foreach (var kind in new[] { ResourceKind.PublishedStatus, ResourceKind.Scores }) { var bad = new World(); Denied(await bad.Host.ReadAsync(User, Request(kind, 1)), "T09 singleton pagination", McpOutcome.InvalidRequest); }
        var deadline = new World(); deadline.Source.Block = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var pending = deadline.Host.ReadAsync(User, Request()); await deadline.Source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2)); deadline.Clock.Value = TimeSpan.FromSeconds(5); deadline.Source.Block.SetResult(); Denied(await pending, "T09 exact deadline", McpOutcome.DependencyUnavailable);
        foreach (var target in new[] { 262143, 262144, 262145 })
        {
            var boundary = new World(); boundary.CoverageAtEnvelopeBytes(target); var actual = await boundary.Host.ReadAsync(User, Request(ResourceKind.Coverage, 100));
            if (target <= 262144) { Check(actual.Outcome == McpOutcome.Success && actual.Envelope.Length == target, "T09 independent exact UTF8 byte boundary " + target); using var bytesDoc = JsonDocument.Parse(actual.Envelope.AsMemory()); Check(bytesDoc.RootElement.GetProperty("items")[1].GetProperty("label").GetString()!.Contains("résumé", StringComparison.Ordinal), "T09 byte edge contains raw multibyte text"); }
            else Denied(actual, "T09 max plus1 byte cap");
        }
        var oversized = new World(); oversized.AddCoverage(100, true); var result = await oversized.Host.ReadAsync(User, Request(ResourceKind.Coverage, 100)); Denied(result, "T09 UTF8 response cap"); Check(result.Outcome is McpOutcome.Limited or McpOutcome.Unavailable, "T09 oversize safe typed denial");
    }
    private static async Task T10()
    {
        var world = new World(); world.Audit.Fail = true; Denied(await world.Host.ReadAsync(User, Request(size: 1)), "T10 audit unavailable", McpOutcome.DependencyUnavailable); Check(world.Audit.Failures.SequenceEqual([OperationalFailure.AuditUnavailable]), "T10 safe operational signal"); world.Audit.Fail = false; var first = await world.Host.ReadAsync(User, Request(size: 1)); Success(first, "T10 audit recovery");
        var throwing = new World(); throwing.Audit.Throw = true; Denied(await throwing.Host.ReadAsync(User, Request()), "T10 audit exception", McpOutcome.DependencyUnavailable); Check(throwing.Audit.Failures.Contains(OperationalFailure.AuditUnavailable), "T10 audit exception signal");
        var retry = new World(); await retry.Host.ReadAsync(User, Request()); retry.Policy.Change(p => p.ActiveAssignment = false); Denied(await retry.Host.ReadAsync(User, Request()), "T10 retry rechecks", McpOutcome.Unavailable); Check(retry.Audit.Events.Count == 2 && retry.Audit.Events[0].Outcome == McpOutcome.Success && retry.Audit.Events[1].Outcome == McpOutcome.Unavailable, "T10 separate completion per retry");
        foreach (var audit in retry.Audit.Events) Check(audit.CorrelationId == User.CorrelationId && audit.ElapsedMilliseconds >= 0, "T10 trusted correlation and monotonic latency");
        var unknown = new World(); await unknown.Host.ReadAsync(User, Encoding.UTF8.GetBytes("{\"resourceKind\":\"PROTECTED-SENTINEL\"}")); Check(unknown.Audit.Events.Count == 1 && unknown.Audit.Events[0].ResourceKind is null && !unknown.Audit.Serialized().Contains("PROTECTED-SENTINEL", StringComparison.Ordinal), "T10 safe unknown operation");
    }
    private static async Task T11()
    {
        foreach (var kind in Enum.GetValues<ResourceKind>())
        {
            var world = new World(); var root = Success(await world.Host.ReadAsync(User, Request(kind)), "T11 " + kind); var bindings = root.GetProperty("bindings");
            using var manifest = JsonDocument.Parse(world.Source.Manifest);
            var scalar = manifest.RootElement.EnumerateObject().Where(p => p.Name is not "items" and not "collections").ToArray();
            Check(bindings.EnumerateObject().Count() == scalar.Length, "T11 exact flattened binding schema");
            foreach (var field in scalar) Check(bindings.GetProperty(field.Name).GetString() == field.Value.GetString(), "T11 frozen " + field.Name);
        }
        var unavailable = new World(); var score = JsonNode.Parse(unavailable.Source.Payloads["syn-score"])!; score["health"] = null; score["maturity"] = null; score["reason"] = "Unavailable"; unavailable.UpdatePayload("syn-score", score);
        var result = await unavailable.Host.ReadAsync(User, Request(ResourceKind.Scores)); Check(result.Outcome == McpOutcome.Success, "T11 explicit unavailable scores accepted"); using var doc = JsonDocument.Parse(result.Envelope.AsMemory()); Check(doc.RootElement.GetProperty("items")[0].GetProperty("health").ValueKind == JsonValueKind.Null, "T11 unavailable score not zero");
    }

    internal sealed class Clock : IMonotonicClock { public TimeSpan Value; public bool Throw; public TimeSpan Elapsed => Throw ? throw new InvalidOperationException("PROTECTED-SENTINEL") : Value; }
    internal sealed class Audit : IMcpAudit
    {
        public bool Fail, Throw; public List<McpAuditEvent> Events { get; } = []; public List<OperationalFailure> Failures { get; } = [];
        public bool Complete(McpAuditEvent entry) { lock (Events) { if (Throw) throw new InvalidOperationException("PROTECTED-SENTINEL"); if (Fail) return false; Events.Add(entry); return true; } }
        public void OperationalFailure(OperationalFailure failure, Guid correlationId) { lock (Failures) Failures.Add(failure); }
        public string Serialized() => JsonSerializer.Serialize(Events);
    }
    internal sealed class Policy : IReadPolicy
    {
        private readonly object fence = new(); public Dictionary<string, Scope> IdentityScopes = []; public Dictionary<string, string> ScopeDigests = []; public long Revision; public string Digest = GoldenDigest; public bool NullGrant, ActiveIdentity = true, ActiveAssignment = true, CustomerPolicy = true, ResourceAvailable = true, ReadBlocked, ActionAllowed = true;
        public ImmutableHashSet<EvidenceCategory> Categories = Enum.GetValues<EvidenceCategory>().ToImmutableHashSet(); public ImmutableHashSet<string>? FieldsOverride; public ReferenceOverlay Overlay = new(Availability.Available, SafeReason.None);
        public ReadGrant? GetGrant(Caller caller, ReadRequest request) { lock (fence) return NullGrant ? null : new(caller.Kind, caller.IdentityId, Revision, IdentityScopes.GetValueOrDefault(caller.IdentityId, GoldenScope), request.Kind, request.AssessmentId, request.ReportVersionId, ScopeDigests.GetValueOrDefault(IdentityScopes.GetValueOrDefault(caller.IdentityId, GoldenScope).CustomerId, Digest), ActiveIdentity, ActiveAssignment, CustomerPolicy, ResourceAvailable, ReadBlocked, ActionAllowed, Categories, FieldsOverride ?? Schema[request.Kind].ToImmutableHashSet()); }
        public ReferenceOverlay GetReferenceAvailability(ReadGrant grant, string itemId) { lock (fence) return Overlay; }
        public bool TryCommit(ReadGrant grant, Action action) { lock (fence) { if (grant.Revision != Revision) return false; action(); return true; } }
        public void Change(Action<Policy> change) { lock (fence) { change(this); Revision++; } }
    }
    internal sealed class Source : IPublicationReader
    {
        public byte[] Manifest = File.ReadAllBytes(World.PathOf("manifest.json")); public string DeclaredDigest = GoldenDigest; public Dictionary<string, byte[]> Payloads = Directory.GetFiles(World.PathOf(""), "syn-*.json").ToDictionary(path => Path.GetFileNameWithoutExtension(path)!, File.ReadAllBytes);
        public Dictionary<string, ManifestSource> OtherManifests = []; public string BlockCustomer = GoldenScope.CustomerId; public int ManifestReads, ItemReads; public bool Absent, Throw; public List<string> ReadIds = []; public TaskCompletionSource? Block; public TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async ValueTask<ManifestSource?> ReadManifestAsync(Scope scope, string assessmentId, string reportVersionId, CancellationToken token) { Interlocked.Increment(ref ManifestReads); Entered.TrySetResult(); if (Block is not null && scope.CustomerId == BlockCustomer) await Block.Task.WaitAsync(token); if (Throw) throw new InvalidOperationException("PROTECTED-SENTINEL"); if (Absent) return null; return OtherManifests.TryGetValue(scope.CustomerId, out var other) ? other : new(ImmutableArray.CreateRange(Manifest), DeclaredDigest); }
        public ValueTask<ImmutableArray<byte>?> ReadItemAsync(Scope scope, string reportVersionId, ResourceKind kind, string itemId, CancellationToken token) { token.ThrowIfCancellationRequested(); Interlocked.Increment(ref ItemReads); lock (ReadIds) ReadIds.Add(itemId); return ValueTask.FromResult<ImmutableArray<byte>?>(Payloads.TryGetValue(itemId, out var bytes) ? ImmutableArray.CreateRange(bytes) : null); }
    }
    internal sealed class World
    {
        public readonly Source Source = new(); public readonly Policy Policy = new(); public readonly Audit Audit = new(); public readonly Clock Clock = new(); public McpHarness Host;
        public World() { Host = new(Source, Policy, Audit, Clock); }
        public static string PathOf(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);
        public void AddCustomer(string identityId) { var scope = new Scope("syn-customer-b", "syn-project-b", "syn-environment-b"); var manifest = JsonNode.Parse(Source.Manifest)!; manifest["customerId"] = scope.CustomerId; manifest["projectId"] = scope.ProjectId; manifest["environmentId"] = scope.EnvironmentId; var bytes = Canonical(manifest); var digest = Hash(bytes); Source.OtherManifests[scope.CustomerId] = new(ImmutableArray.CreateRange(bytes), digest); Policy.IdentityScopes[identityId] = scope; Policy.ScopeDigests[scope.CustomerId] = digest; }
        public void Rebind() { Source.DeclaredDigest = Policy.Digest = Hash(Source.Manifest); }
        public void UpdatePayload(string id, JsonNode node) { Source.Payloads[id] = Canonical(node); var manifest = JsonNode.Parse(Source.Manifest)!; foreach (var descriptor in manifest["items"]!.AsArray()) if (descriptor!["itemId"]!.GetValue<string>() == id) descriptor["payloadDigest"] = Hash(Source.Payloads[id]); Source.Manifest = Canonical(manifest); Rebind(); }
        public void RemoveKind(ResourceKind kind) { var manifest = JsonNode.Parse(Source.Manifest)!; var items = manifest["items"]!.AsArray(); for (var i = items.Count - 1; i >= 0; i--) if (items[i]!["kind"]!.GetValue<string>() == kind.ToString()) items.RemoveAt(i); Source.Manifest = Canonical(manifest); Rebind(); }
        private byte[] ExpectedCoverageEnvelope()
        {
            var manifest = JsonNode.Parse(Source.Manifest)!; var bindings = new JsonObject(); foreach (var field in manifest.AsObject()) if (field.Key is not "collections" and not "items") bindings[field.Key] = field.Value!.DeepClone();
            var items = new JsonArray(manifest["items"]!.AsArray().Where(d => d!["kind"]!.GetValue<string>() == "Coverage").Select(d => JsonNode.Parse(Source.Payloads[d!["itemId"]!.GetValue<string>()])!).ToArray());
            return Canonical(new JsonObject { ["bindings"] = bindings, ["contractVersion"] = "synthetic-published-health-read-v1", ["resourceKind"] = "Coverage", ["manifestDigest"] = Policy.Digest, ["items"] = items, ["nextCursor"] = null });
        }
        public void CoverageAtEnvelopeBytes(int target)
        {
            AddCoverage(100, true);
            for (var i = 0; i < 100; i++) { var id = "syn-coverage-" + i.ToString("d4", CultureInfo.InvariantCulture); var item = JsonNode.Parse(Source.Payloads[id])!; item["label"] = i == 0 ? "Fictional finding." : "Fictional résumé."; UpdatePayload(id, item); }
            var delta = target - ExpectedCoverageEnvelope().Length; var firstExtra = Enumerable.Range(0, 20).Single(n => (delta - 19 * n) % 20 == 0); var firstId = "syn-coverage-0000"; var first = JsonNode.Parse(Source.Payloads[firstId])!; first["label"] = string.Join(" ", Enumerable.Repeat("Fictional finding.", firstExtra + 1)); UpdatePayload(firstId, first); delta -= 19 * firstExtra;
            for (var i = 1; i < 100; i++) { var extra = Math.Min(200, delta / 20); delta -= extra * 20; var id = "syn-coverage-" + i.ToString("d4", CultureInfo.InvariantCulture); var item = JsonNode.Parse(Source.Payloads[id])!; item["label"] = string.Join(" ", Enumerable.Repeat("Fictional résumé.", extra + 1)); UpdatePayload(id, item); }
            Check(delta == 0 && ExpectedCoverageEnvelope().Length == target, "T09 independent expected UTF8 envelope construction " + target);
        }
        public void AddCoverage(int count, bool unicode) { RemoveKind(ResourceKind.Coverage); var manifest = JsonNode.Parse(Source.Manifest)!; var descriptors = manifest["items"]!.AsArray(); for (var i = 0; i < count; i++) { var id = "syn-coverage-" + i.ToString("d4", CultureInfo.InvariantCulture); var atom = unicode ? "Fictional résumé." : "Fictional coverage."; var label = string.Join(" ", Enumerable.Repeat(atom, 220)); var item = new JsonObject { ["itemId"] = id, ["category"] = "Summary", ["assessed"] = 1, ["gap"] = 0, ["unavailable"] = 0, ["label"] = label, ["limitations"] = new JsonArray() }; Source.Payloads[id] = Canonical(item); descriptors.Add(new JsonObject { ["kind"] = "Coverage", ["itemId"] = id, ["category"] = "Summary", ["payloadDigest"] = Hash(Source.Payloads[id]) }); } var ordered = descriptors.Select(d => d!.DeepClone()).OrderBy(d => Array.IndexOf(Kinds, d["kind"]!.GetValue<string>())).ThenBy(d => d["itemId"]!.GetValue<string>(), StringComparer.Ordinal).ToArray(); manifest["items"] = new JsonArray(ordered); Source.Manifest = Canonical(manifest); Rebind(); }
    }
}
