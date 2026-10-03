using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using SyntheticMcp;

// This fixture authoring uses independent JSON writing/hash primitives, never the module codec.
internal sealed class Fixture : IPublicationReader, IReadPolicy, IMcpAudit, IMonotonicClock
{
    internal readonly object PolicyFence = new();
    internal readonly Scope Scope = new("syn-customer", "syn-project", "syn-environment");
    internal readonly Caller Caller = new(IdentityKind.NamedUser, "syn-user", Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
    internal readonly Dictionary<(ResourceKind, string), ImmutableArray<byte>> Payloads = new();
    internal ManifestSource Manifest;
    internal Func<ReadGrant, ReadGrant?> ChangeGrant = g => g;
    internal Action? BeforeCommit;
    internal long Revision = 1;
    internal bool MissingSource, ThrowSource, ThrowPolicy, ThrowAudit, AuditAvailable = true, ThrowClock;
    internal TimeSpan Time;
    internal TaskCompletionSource? Block;
    internal int ManifestReads, ItemReads, PolicyCalls;
    internal readonly List<McpAuditEvent> Events = new();
    internal readonly List<OperationalFailure> Failures = new();
    internal readonly Dictionary<string, ManifestSource> ScopedManifests = new(StringComparer.Ordinal);
    internal ManualResetEventSlim? AuditGate;
    internal CountdownEvent? AuditsEntered;
    internal readonly McpHarness Harness;

    internal Fixture(int coverage = 3, int textRepeat = 1, bool unicode = false)
    {
        var descriptors = new List<object>();
        Add(ResourceKind.PublishedStatus, "syn-status", new { approvalState = "SyntheticApproved", assessmentState = "CompletedWithGaps", category = "Summary", itemId = "syn-status", limitations = new[] { "DeclaredGap" } });
        for (var i = 0; i < coverage; i++)
        {
            var id = $"syn-coverage-{i:d4}";
            var atom = unicode ? "Fictional résumé." : "Fictional coverage.";
            Add(ResourceKind.Coverage, id, new { assessed = 12, category = "Summary", gap = 1, itemId = id, label = string.Join(' ', Enumerable.Repeat(atom, textRepeat)), limitations = new[] { "DeclaredGap" }, unavailable = 2 });
        }
        Add(ResourceKind.Scores, "syn-scores", new { category = "Summary", health = 61.25m, itemId = "syn-scores", maturity = 3, quality = 72.5m, reason = "DeclaredGap" });
        var manifest = new Dictionary<string, object?>
        {
            ["contractVersion"] = "synthetic-published-health-read-v1",
            ["fixtureKind"] = "SyntheticPublishedFixture",
            ["customerId"] = Scope.CustomerId,
            ["projectId"] = Scope.ProjectId,
            ["environmentId"] = Scope.EnvironmentId,
            ["assessmentId"] = "syn-assessment",
            ["reportVersionId"] = "syn-report",
            ["baselineVersion"] = "syn-baseline",
            ["catalogVersion"] = "syn-catalog",
            ["scoringProfileVersion"] = "syn-scoring",
            ["maturityProfileVersion"] = "syn-maturity",
            ["applicationVersion"] = "syn-app",
            ["assessmentState"] = "CompletedWithGaps",
            ["approvalState"] = "SyntheticApproved",
            ["collections"] = new[] { "PublishedStatus", "Coverage", "Scores", "Findings", "Recommendations", "ProtectedReferences" },
            ["items"] = descriptors
        };
        var bytes = Canonical(manifest);
        Manifest = new(bytes.ToImmutableArray(), Hash(bytes));
        Harness = new(this, this, this, this);

        void Add(ResourceKind kind, string id, object value)
        {
            var bytes = Canonical(value);
            Payloads.Add((kind, id), bytes.ToImmutableArray());
            descriptors.Add(new { kind = kind.ToString(), itemId = id, category = "Summary", payloadDigest = Hash(bytes) });
        }
    }
    internal static byte[] Canonical(object value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            Write(JsonSerializer.SerializeToElement(value), writer);
        }
        return stream.ToArray();
    }
    private static void Write(JsonElement value, Utf8JsonWriter writer)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)) { writer.WritePropertyName(property.Name); Write(property.Value, writer); }
                writer.WriteEndObject(); break;
            case JsonValueKind.Array:
                writer.WriteStartArray(); foreach (var item in value.EnumerateArray()) Write(item, writer); writer.WriteEndArray(); break;
            case JsonValueKind.Number: writer.WriteRawValue(value.GetDecimal().ToString("0.############################", CultureInfo.InvariantCulture)); break;
            default: value.WriteTo(writer); break;
        }
    }
    internal static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal ManifestSource ForCustomer(string customer)
    {
        if (ScopedManifests.TryGetValue(customer, out var result)) return result;
        using var original = JsonDocument.Parse(Manifest.Bytes.AsMemory());
        var values = original.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value.Clone(), StringComparer.Ordinal);
        values["customerId"] = customer;
        var bytes = Canonical(values);
        result = new(bytes.ToImmutableArray(), Hash(bytes));
        ScopedManifests.Add(customer, result);
        return result;
    }
    internal static byte[] Request(ResourceKind kind = ResourceKind.Coverage, int? size = null, string? cursor = null)
    {
        var value = new Dictionary<string, object?> { ["contractVersion"] = "synthetic-published-health-read-v1", ["resourceKind"] = kind.ToString(), ["assessmentId"] = "syn-assessment", ["reportVersionId"] = "syn-report" };
        if (size is not null) value["pageSize"] = size;
        if (cursor is not null) value["cursor"] = cursor;
        return JsonSerializer.SerializeToUtf8Bytes(value);
    }
    internal Task<McpResult> Read(byte[]? request = null, Caller? caller = null, CancellationToken token = default) => Harness.ReadAsync(caller ?? Caller, request ?? Request(), token);
    public TimeSpan Elapsed => ThrowClock ? throw new InvalidOperationException("protected-clock-sentinel") : Time;
    public ReadGrant? GetGrant(Caller caller, ReadRequest request)
    {
        Interlocked.Increment(ref PolicyCalls);
        lock (PolicyFence)
        {
            if (ThrowPolicy) throw new InvalidOperationException("protected-policy-sentinel");
            return ChangeGrant(new(caller.Kind, caller.IdentityId, Revision, Scope, request.Kind, request.AssessmentId, request.ReportVersionId, Manifest.Digest,
                true, true, true, true, false, true, Enum.GetValues<EvidenceCategory>().ToImmutableHashSet(), Fields(request.Kind)));
        }
    }
    private static ImmutableHashSet<string> Fields(ResourceKind kind) => (kind switch
    {
        ResourceKind.PublishedStatus => new[] { "itemId", "category", "assessmentState", "approvalState", "limitations" },
        ResourceKind.Coverage => ["itemId", "category", "assessed", "gap", "unavailable", "label", "limitations"],
        ResourceKind.Scores => ["itemId", "category", "health", "quality", "maturity", "reason"],
        ResourceKind.Findings => ["itemId", "category", "title", "summary", "severity", "reviewState", "confidence", "mandatoryReview", "referenceIds"],
        ResourceKind.Recommendations => ["itemId", "category", "findingId", "summary", "options", "priority", "effort", "reviewLabel"],
        ResourceKind.ProtectedReferences => ["itemId", "category", "availability", "reason", "currentAvailability", "availabilityReason"],
        _ => []
    }).ToImmutableHashSet(StringComparer.Ordinal);
    public ReferenceOverlay GetReferenceAvailability(ReadGrant grant, string itemId) => new(Availability.Unavailable, SafeReason.Expired);
    public bool TryCommit(ReadGrant grant, Action commit)
    {
        lock (PolicyFence)
        {
            BeforeCommit?.Invoke();
            if (grant.Revision != Revision) return false;
            commit();
            return true;
        }
    }
    public async ValueTask<ManifestSource?> ReadManifestAsync(Scope scope, string assessmentId, string reportVersionId, CancellationToken token)
    {
        Interlocked.Increment(ref ManifestReads);
        if (Block is not null) await Block.Task; // Deliberately ignores cancellation to test boundary deadline/release.
        if (ThrowSource) throw new InvalidOperationException("protected-source-sentinel");
        return MissingSource ? null : ScopedManifests.GetValueOrDefault(scope.CustomerId, Manifest);
    }
    public ValueTask<ImmutableArray<byte>?> ReadItemAsync(Scope scope, string reportVersionId, ResourceKind kind, string itemId, CancellationToken token)
    {
        Interlocked.Increment(ref ItemReads);
        return ValueTask.FromResult<ImmutableArray<byte>?>(Payloads.GetValueOrDefault((kind, itemId)));
    }
    public bool Complete(McpAuditEvent auditEvent)
    {
        lock (Events) Events.Add(auditEvent);
        if (auditEvent.Outcome == McpOutcome.Unavailable && AuditGate is not null)
        {
            AuditsEntered?.Signal();
            AuditGate.Wait();
        }
        if (ThrowAudit) throw new InvalidOperationException("protected-audit-sentinel");
        return AuditAvailable;
    }
    public void OperationalFailure(OperationalFailure failure, Guid correlationId) { lock (Failures) Failures.Add(failure); }
}
