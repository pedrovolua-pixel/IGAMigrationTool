using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ReportPublication;

// Owns fictional source construction after full field/category admission. No host/source endpoint.
internal static class FixtureSourceDecoder
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    internal static SourceCaptureV1 Decode(ReadOnlyMemory<byte> sourceBytes, ReadOnlyMemory<byte> metadataBytes)
    {
        RejectDuplicatesAndInvalidUnicode(sourceBytes.Span);
        RejectDuplicatesAndInvalidUnicode(metadataBytes.Span);
        var sourceNode = JsonNode.Parse(StrictUtf8.GetString(sourceBytes.Span)) ?? throw Refusal();
        SourceCaptureV1 capture;
        try { capture = FrozenFixtureLoader.Source(sourceNode); }
        catch (Exception) { throw Refusal(); }
        try
        {
            if (!NativePublicationCanonicalV1.SourceBytes(capture).AsSpan().SequenceEqual(sourceBytes.Span)) throw Refusal();
        }
        catch (Exception) { throw Refusal(); }
        // Canonical equality proves every closed owning source property, including redundant root inputs/score/warnings/scope.
        if (!MetadataFor(sourceNode, capture).AsSpan().SequenceEqual(metadataBytes.Span)) throw Refusal();
        return capture;
    }
    internal static byte[] MetadataFor(JsonNode sourceNode, SourceCaptureV1 capture)
    {
        var source = NativePublicationCanonicalV1.SourceBytes(capture);
        var references = capture.Projection.TechnicalAppendices.ProtectedReferences.OrderBy(x => x.Id.ToString("D"), StringComparer.Ordinal);
        var metadata = new JsonObject
        {
            ["schemaVersion"] = "native-publication-fixture-source-metadata-v1",
            ["scope"] = sourceNode["scope"]!.DeepClone(),
            ["runId"] = sourceNode["runId"]!.DeepClone(),
            ["runRevision"] = sourceNode["runRevision"]!.DeepClone(),
            ["assessmentState"] = sourceNode["assessmentState"]!.DeepClone(),
            ["sourceDigest"] = NativePublicationCanonicalV1.Hash(source),
            ["projectionDigest"] = NativePublicationCanonicalV1.Hash(NativePublicationCanonicalV1.ProjectionBytes(capture.Projection)),
            ["scoreDigest"] = NativePublicationCanonicalV1.Hash(NativePublicationCanonicalV1.ScoreBytes(capture.Projection.Scores)),
            ["requiredCategories"] = sourceNode["requiredCategories"]!.DeepClone(),
            ["requiredFields"] = sourceNode["requiredFields"]!.DeepClone(),
            ["inputs"] = sourceNode["inputs"]!.DeepClone(),
            ["retention"] = sourceNode["retention"]!.DeepClone(),
            ["provenance"] = sourceNode["provenance"]!.DeepClone(),
            ["warnings"] = sourceNode["warnings"]!.DeepClone(),
            ["protectedReferences"] = new JsonArray(references.Select(x => (JsonNode)new JsonObject { ["id"] = x.Id.ToString("D"), ["category"] = x.Category }).ToArray())
        };
        return CanonicalFixtureControl(metadata);
    }
    internal static byte[] CanonicalFixtureControl(JsonNode value)
    {
        using var document = JsonDocument.Parse(value.ToJsonString());
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, document.RootElement);
        return stream.ToArray();
    }
    private static void Write(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var item in value.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
                { writer.WritePropertyName(item.Name); Write(writer, item.Value); }
                writer.WriteEndObject(); break;
            case JsonValueKind.Array:
                writer.WriteStartArray(); foreach (var item in value.EnumerateArray()) Write(writer, item); writer.WriteEndArray(); break;
            case JsonValueKind.String:
                var text = value.GetString()!;
                if (text.Any(x => x < 32 || x > 126 || x is '"' or '\\')) throw Refusal();
                writer.WriteStringValue(text); break;
            case JsonValueKind.True: writer.WriteBooleanValue(true); break;
            case JsonValueKind.False: writer.WriteBooleanValue(false); break;
            case JsonValueKind.Null: writer.WriteNullValue(); break;
            default: throw Refusal();
        }
    }
    private static void RejectDuplicatesAndInvalidUnicode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            _ = StrictUtf8.GetString(bytes);
            using var document = JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
            Walk(document.RootElement);
        }
        catch (Exception) { throw Refusal(); }
    }
    private static void Walk(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in value.EnumerateObject()) { if (!names.Add(item.Name)) throw Refusal(); Walk(item.Value); }
        }
        else if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) Walk(item);
        else if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString()!;
            for (var i = 0; i < text.Length; i++)
                if (char.IsSurrogate(text[i]))
                {
                    if (!char.IsHighSurrogate(text[i]) || i + 1 >= text.Length || !char.IsLowSurrogate(text[++i])) throw Refusal();
                }
        }
    }
    private static PublicationIntegrityException Refusal() => new();
}
