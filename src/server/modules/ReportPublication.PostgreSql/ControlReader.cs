using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using ReportPublication;

namespace ReportPublication.PostgreSql;

/// <summary>Closed ASCII metadata reader. It cannot create an owning source or permission.</summary>
internal static class NativeControlReaderV1
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    internal const string SchemaVersion = "native-publication-postgresql-fixture-v1";
    internal static DateTimeOffset Time(string value)
    {
        if (!DateTimeOffset.TryParseExact(value, "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            || parsed.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture) != value) throw Integrity();
        return parsed;
    }
    internal static PublicationManifestV1 Manifest(ReadOnlyMemory<byte> bytes) => ReadOwned(bytes, ManifestCore);
    private static PublicationManifestV1 ManifestCore(ReadOnlyMemory<byte> bytes)
    {
        using var document = Parse(bytes, 1024 * 1024);
        var v = document.RootElement;
        Constant(v, "schemaVersion", "health-report-manifest-v1");
        Constant(v, "projectionSchemaVersion", "health-report-projection-v1");
        Constant(v, "scoreSchemaVersion", "health-report-score-v1");
        Constant(v, "classification", "MinimizedDerivedReport");
        var scope = Scope(v.GetProperty("scope"), Id(v, "assessmentId"));
        var creator = v.GetProperty("createdBy");
        var value = new PublicationManifestV1(scope, Id(v, "reportVersionId"), Id(v, "runId"), Counter(v, "runRevision"),
            Time(Text(v, "createdAtUtc")), new(Id(creator, "tenantId"), Id(creator, "objectId")),
            Named<AssessmentStateV1>(v, "assessmentState"), Named<PublicationApprovalV1>(v, "approvalState"),
            Inputs(v.GetProperty("inputs")), Digest(v, "projectionDigest"), Digest(v, "scoreDigest"), Digest(v, "sourceDigest"),
            Tokens(v.GetProperty("requiredCategories")), Tokens(v.GetProperty("requiredFields")),
            Array(v, "redactionMarkers").Select(r => new PublicationRedactionMarkerV1(Text(r, "section"), Id(r, "id"),
                Text(r, "field"), Named<PublicationReasonV1>(r, "reason"))), Retention(v.GetProperty("retention")),
            Provenance(v), Array(v, "artifactInputs").Select(a => new PublicationBlobDescriptorV1(
                Named<PublicationBlobKindV1>(a, "kind"), Digest(a, "digest"), Counter(a, "byteLength"))));
        Same(bytes, NativePublicationCanonicalV1.ManifestBytes(value));
        return value;
    }
    internal static PublicationReceiptV1 PublicationReceipt(ReadOnlyMemory<byte> bytes) => ReadOwned(bytes, PublicationReceiptCore);
    private static PublicationReceiptV1 PublicationReceiptCore(ReadOnlyMemory<byte> bytes)
    {
        using var document = Parse(bytes, NativePublicationCanonicalV1.MaximumCommandBytes);
        var v = document.RootElement;
        Constant(v, "schemaVersion", "report-publication-receipt-v1");
        var value = new PublicationReceiptV1(Id(v, "operationId"), Actor(v.GetProperty("actor")), Scope(v.GetProperty("scope")),
            Id(v, "runId"), Counter(v, "expectedRunRevision"), Digest(v, "commandDigest"), Id(v, "reportVersionId"),
            Digest(v, "manifestDigest"), Digest(v, "projectionDigest"), Digest(v, "scoreDigest"),
            Time(Text(v, "committedAtUtc")), Id(v, "eventId"), Digest(v, "eventDigest"));
        Same(bytes, NativePublicationCanonicalV1.PublicationReceiptBytes(value));
        return value;
    }
    internal static ExactReadReceiptV1 ReadReceipt(ReadOnlyMemory<byte> bytes) => ReadOwned(bytes, ReadReceiptCore);
    private static ExactReadReceiptV1 ReadReceiptCore(ReadOnlyMemory<byte> bytes)
    {
        using var document = Parse(bytes, NativePublicationCanonicalV1.MaximumCommandBytes);
        var v = document.RootElement;
        Constant(v, "schemaVersion", "report-exact-read-receipt-v1");
        var value = new ExactReadReceiptV1(Id(v, "invocationId"), Digest(v, "requestDigest"), Actor(v.GetProperty("actor")),
            Scope(v.GetProperty("scope")), Id(v, "reportVersionId"), Digest(v, "manifestDigest"),
            Time(Text(v, "committedAtUtc")), Id(v, "eventId"), Digest(v, "eventDigest"));
        Same(bytes, NativePublicationCanonicalV1.ReadReceiptBytes(value));
        return value;
    }
    internal static PublicationAuditEventV1 Audit(ReadOnlyMemory<byte> bytes) => ReadOwned(bytes, AuditCore);
    private static PublicationAuditEventV1 AuditCore(ReadOnlyMemory<byte> bytes)
    {
        using var document = Parse(bytes, NativePublicationCanonicalV1.MaximumCommandBytes);
        var v = document.RootElement;
        Constant(v, "schemaVersion", "report-publication-audit-v1");
        var value = new PublicationAuditEventV1(Id(v, "eventId"), Id(v, "streamId"), Id(v, "writerBindingReference"),
            Counter(v, "sequence"), Digest(v, "previousEventDigest"), Time(Text(v, "eventAtUtc")), NullableId(v, "operationId"),
            Id(v, "invocationId"), Id(v, "correlationId"), Named<PublicationAuditActorKindV1>(v, "actorKind"),
            v.GetProperty("actor").ValueKind == JsonValueKind.Null ? null : Actor(v.GetProperty("actor")),
            v.GetProperty("scope").ValueKind == JsonValueKind.Null ? null : Scope(v.GetProperty("scope")),
            NullableNamed<PublicationResourceKindV1>(v, "resourceKind"), NullableId(v, "resourceId"),
            Named<PublicationAuditActionV1>(v, "action"), Named<PublicationAuditOutcomeV1>(v, "outcome"),
            Named<PublicationAuditReasonV1>(v, "reason"), NullableText(v, "manifestDigest"),
            Tokens(v.GetProperty("returnedFields")), Tokens(v.GetProperty("redactedFields")));
        Same(bytes, NativePublicationCanonicalV1.AuditEventBytes(value));
        return value;
    }
    internal static FixtureSourceMetadataV1 SourceMetadata(ReadOnlyMemory<byte> bytes) => ReadOwned(bytes, SourceMetadataCore);
    private static FixtureSourceMetadataV1 SourceMetadataCore(ReadOnlyMemory<byte> bytes)
    {
        using var document = Parse(bytes, 1024 * 1024);
        var v = document.RootElement;
        Keys(v, "schemaVersion", "scope", "runId", "runRevision", "assessmentState", "sourceDigest", "projectionDigest", "scoreDigest",
            "requiredCategories", "requiredFields", "inputs", "retention", "provenance", "protectedReferences", "warnings");
        Constant(v, "schemaVersion", "native-publication-fixture-source-metadata-v1");
        var value = new FixtureSourceMetadataV1(Scope(v.GetProperty("scope")), Id(v, "runId"), Counter(v, "runRevision"),
            Named<AssessmentStateV1>(v, "assessmentState"), Digest(v, "sourceDigest"), Digest(v, "projectionDigest"), Digest(v, "scoreDigest"),
            Tokens(v.GetProperty("requiredCategories")), Tokens(v.GetProperty("requiredFields")), Inputs(v.GetProperty("inputs")),
            Retention(v.GetProperty("retention")), Provenance(v).ToImmutableArray(),
            Array(v, "protectedReferences").Select(r =>
            {
                Keys(r, "id", "category"); return new FixtureProtectedReferenceV1(Id(r, "id"), Token(Text(r, "category")));
            }).ToImmutableArray(), Array(v, "warnings").Select(w =>
            {
                Keys(w, "kind", "recordId", "category"); return new PublicationWarningV1(Named<PublicationWarningKindV1>(w, "kind"),
                    Id(w, "recordId"), Token(Text(w, "category")));
            }).ToImmutableArray());
        if (!PublicationValidationV1.Scope(value.Scope) || value.RequiredCategories.IsEmpty
            || !value.RequiredFields.SequenceEqual(PublicationValidationV1.RequiredFields)
            || value.Provenance.Select(p => (p.Kind, p.OpaqueRecordId)).Distinct().Count() != value.Provenance.Length
            || !value.Provenance.SequenceEqual(value.Provenance.OrderBy(p => p.Kind, StringComparer.Ordinal)
                .ThenBy(p => p.OpaqueRecordId.ToString("D"), StringComparer.Ordinal))
            || !Ordered(value.ProtectedReferences.Select(r => r.Id.ToString("D")))
            || !Ordered(value.Warnings.Select(w => w.Kind + ":" + w.RecordId.ToString("D") + ":" + w.Category))) throw Integrity();
        Same(bytes, CanonicalTree(v));
        return value;
    }
    internal static PublicationScopeV1 Scope(JsonElement v, Guid? assessmentId = null)
    {
        if (assessmentId is null) Keys(v, "customerId", "projectId", "environmentId", "assessmentId");
        else Keys(v, "customerId", "projectId", "environmentId");
        return new(Id(v, "customerId"), Id(v, "projectId"), Id(v, "environmentId"), assessmentId ?? Id(v, "assessmentId"));
    }
    internal static PublicationActorV1 Actor(JsonElement v)
    {
        Keys(v, "tenantId", "objectId", "sessionId", "securityVersion");
        return new(Id(v, "tenantId"), Id(v, "objectId"), Id(v, "sessionId"), Counter(v, "securityVersion"));
    }
    private static PublicationInputsV1 Inputs(JsonElement v)
    {
        Keys(v, "baselineId", "baselineDigest", "capabilityLockDigest", "ruleCatalogVersion", "ruleCatalogDigest", "scoringProfileVersion",
            "scoringProfileDigest", "maturityProfileVersion", "maturityProfileDigest", "desiredOutcomeVersion", "desiredOutcomeDigest",
            "scoringAlgorithmVersion", "maturityAlgorithmVersion", "applicationVersion", "reviewSnapshotDigest", "coverageSnapshotDigest",
            "runInputDigest", "aiPolicyVersion", "modelVersion", "promptVersion");
        var desiredVersion = NullableText(v, "desiredOutcomeVersion"); var desiredDigest = NullableText(v, "desiredOutcomeDigest");
        if ((desiredVersion is null) != (desiredDigest is null) || desiredVersion is not null && (!IsToken(desiredVersion)
            || !PublicationValidationV1.Digest(desiredDigest))) throw Integrity();
        return new(Id(v, "baselineId"), Digest(v, "baselineDigest"), Digest(v, "capabilityLockDigest"), Token(Text(v, "ruleCatalogVersion")),
            Digest(v, "ruleCatalogDigest"), Token(Text(v, "scoringProfileVersion")), Digest(v, "scoringProfileDigest"),
            Token(Text(v, "maturityProfileVersion")), Digest(v, "maturityProfileDigest"), desiredVersion, desiredDigest,
            Token(Text(v, "scoringAlgorithmVersion")), Token(Text(v, "maturityAlgorithmVersion")), Token(Text(v, "applicationVersion")),
            Digest(v, "reviewSnapshotDigest"), Digest(v, "coverageSnapshotDigest"), Digest(v, "runInputDigest"), Token(Text(v, "aiPolicyVersion")),
            NullableToken(v, "modelVersion"), NullableToken(v, "promptVersion"));
    }
    private static PublicationRetentionV1 Retention(JsonElement v)
    {
        Keys(v, "policyId", "policyVersion", "class", "clockStartUtc", "expiresAtUtc", "holdReference");
        Constant(v, "class", "PublishedArtifact");
        var start = Time(Text(v, "clockStartUtc")); var end = Time(Text(v, "expiresAtUtc"));
        if (start > end) throw Integrity();
        return new(Id(v, "policyId"), Token(Text(v, "policyVersion")), start, end, NullableId(v, "holdReference"));
    }
    private static IEnumerable<PublicationProvenanceV1> Provenance(JsonElement v) => Array(v, "provenance").Select(p =>
    {
        Keys(p, "kind", "opaqueRecordId", "digest");
        return new PublicationProvenanceV1(Token(Text(p, "kind")), Id(p, "opaqueRecordId"), Digest(p, "digest"));
    });
    internal static void Same(ReadOnlyMemory<byte> actual, ReadOnlySpan<byte> expected)
    { if (!actual.Span.SequenceEqual(expected)) throw Integrity(); }
    internal static Guid Id(JsonElement v, string key)
    {
        var raw = Text(v, key);
        if (!Guid.TryParseExact(raw, "D", out var id) || id == Guid.Empty || id.ToString("D") != raw) throw Integrity();
        return id;
    }
    internal static string Text(JsonElement v, string key) => Text(v.GetProperty(key));
    private static string Text(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString()! : throw Integrity();
    private static Guid? NullableId(JsonElement v, string key) => v.GetProperty(key).ValueKind == JsonValueKind.Null ? null : Id(v, key);
    private static string? NullableText(JsonElement v, string key) => v.GetProperty(key).ValueKind == JsonValueKind.Null ? null : Text(v, key);
    private static string? NullableToken(JsonElement v, string key) => NullableText(v, key) is { } text ? Token(text) : null;
    private static string Digest(JsonElement v, string key) => PublicationValidationV1.Digest(Text(v, key)) ? Text(v, key) : throw Integrity();
    private static long Counter(JsonElement v, string key)
    {
        var s = Text(v, key);
        if (!long.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value <= 0
            || value.ToString(CultureInfo.InvariantCulture) != s) throw Integrity();
        return value;
    }
    internal static T Named<T>(JsonElement v, string key) where T : struct, Enum
    {
        var text = Text(v, key);
        return Enum.TryParse<T>(text, false, out var value) && Enum.IsDefined(value) && value.ToString() == text ? value : throw Integrity();
    }
    private static T? NullableNamed<T>(JsonElement v, string key) where T : struct, Enum =>
        v.GetProperty(key).ValueKind == JsonValueKind.Null ? null : Named<T>(v, key);
    private static void Constant(JsonElement v, string key, string value)
    { if (Text(v, key) != value) throw Integrity(); }
    private static JsonElement.ArrayEnumerator Array(JsonElement v, string key) => v.GetProperty(key).EnumerateArray();
    internal static ImmutableArray<string> Tokens(JsonElement v)
    {
        var values = v.EnumerateArray().Select(x => Token(Text(x))).ToImmutableArray();
        if (!Ordered(values)) throw Integrity(); return values;
    }
    internal static string Token(string value) => IsToken(value) ? value : throw Integrity();
    private static bool IsToken(string value) => value.Length is >= 1 and <= 128 && value.All(c =>
        c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '_' or '-');
    private static bool Ordered(IEnumerable<string> values)
    {
        string? previous = null;
        foreach (var value in values) { if (previous is not null && StringComparer.Ordinal.Compare(previous, value) >= 0) return false; previous = value; }
        return true;
    }
    private static void Keys(JsonElement v, params string[] expected)
    {
        if (v.ValueKind != JsonValueKind.Object || !v.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)
            .SequenceEqual(expected.Order(StringComparer.Ordinal))) throw Integrity();
    }
    private static JsonDocument Parse(ReadOnlyMemory<byte> input, int maximum)
    {
        try
        {
            if (input.IsEmpty || input.Length > maximum || input.Span.IndexOf((byte)0) >= 0) throw Integrity();
            _ = StrictUtf8.GetString(input.Span);
            foreach (var b in input.Span) if (b > 127) throw Integrity();
            var doc = JsonDocument.Parse(input, new JsonDocumentOptions { MaxDepth = 32, CommentHandling = JsonCommentHandling.Disallow });
            try { var count = 0; Check(doc.RootElement, ref count); return doc; } catch { doc.Dispose(); throw; }
        }
        catch (Exception ex) when (ex is JsonException or DecoderFallbackException or InvalidOperationException or ArgumentException)
        { throw Integrity(); }
    }
    private static void Check(JsonElement v, ref int count)
    {
        if (++count > 100000) throw Integrity();
        if (v.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in v.EnumerateObject()) { if (!seen.Add(property.Name)) throw Integrity(); Check(property.Value, ref count); }
        }
        else if (v.ValueKind == JsonValueKind.Array) foreach (var item in v.EnumerateArray()) Check(item, ref count);
        else if (v.ValueKind == JsonValueKind.Number || v.ValueKind == JsonValueKind.Undefined) throw Integrity();
    }
    private static byte[] CanonicalTree(JsonElement root)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, root);
        return stream.ToArray();
    }
    private static void Write(Utf8JsonWriter w, JsonElement v)
    {
        switch (v.ValueKind)
        {
            case JsonValueKind.Object:
                w.WriteStartObject(); foreach (var p in v.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                { w.WritePropertyName(p.Name); Write(w, p.Value); }
                w.WriteEndObject(); break;
            case JsonValueKind.Array: w.WriteStartArray(); foreach (var x in v.EnumerateArray()) Write(w, x); w.WriteEndArray(); break;
            case JsonValueKind.String: w.WriteStringValue(v.GetString()); break;
            case JsonValueKind.Null: w.WriteNullValue(); break;
            case JsonValueKind.True: w.WriteBooleanValue(true); break;
            case JsonValueKind.False: w.WriteBooleanValue(false); break;
            default: throw Integrity();
        }
    }
    private static T ReadOwned<T>(ReadOnlyMemory<byte> input, Func<ReadOnlyMemory<byte>, T> read)
    {
        // The provider cannot retain an alias that changes bytes between admission and comparison.
        var owned = input.ToArray();
        return Guard(() => read(owned));
    }
    private static T Guard<T>(Func<T> read)
    {
        try { return read(); }
        catch (Exception ex) when (ex is JsonException or DecoderFallbackException or InvalidOperationException
            or ArgumentException or FormatException or OverflowException or KeyNotFoundException)
        { throw Integrity(); }
    }
    private static Exception Integrity() => new PublicationIntegrityException();
}

internal sealed record FixtureProtectedReferenceV1(Guid Id, string Category);
internal sealed record FixtureSourceMetadataV1(PublicationScopeV1 Scope, Guid RunId, long RunRevision, AssessmentStateV1 AssessmentState,
    string SourceDigest, string ProjectionDigest, string ScoreDigest, ImmutableArray<string> RequiredCategories, ImmutableArray<string> RequiredFields,
    PublicationInputsV1 Inputs, PublicationRetentionV1 Retention, ImmutableArray<PublicationProvenanceV1> Provenance,
    ImmutableArray<FixtureProtectedReferenceV1> ProtectedReferences, ImmutableArray<PublicationWarningV1> Warnings);
