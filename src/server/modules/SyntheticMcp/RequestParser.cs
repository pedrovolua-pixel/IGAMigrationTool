using System.Text;
using System.Text.Json;

namespace SyntheticMcp;

internal static class RequestParser
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    internal static ReadRequest? Parse(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length is 0 or > 16384) return null;
        try
        {
            _ = Utf8.GetString(bytes.Span);
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 2 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
                if (!names.Add(property.Name) || property.Name is not ("contractVersion" or "resourceKind" or "assessmentId" or "reportVersionId" or "pageSize" or "cursor")) return null;
            if (!String(root, "contractVersion", out var version) || version != McpContract.Version ||
                !String(root, "resourceKind", out var kindText) || !Enum.TryParse<ResourceKind>(kindText, false, out var kind) ||
                !Enum.IsDefined(kind) || kind.ToString() != kindText ||
                !String(root, "assessmentId", out var assessment) || !PublicationCodec.IsId(assessment) ||
                !String(root, "reportVersionId", out var report) || !PublicationCodec.IsId(report)) return null;
            var pageSize = 25;
            string? cursor = null;
            if (kind is ResourceKind.PublishedStatus or ResourceKind.Scores && (names.Contains("pageSize") || names.Contains("cursor"))) return null;
            if (root.TryGetProperty("pageSize", out var size) && (size.ValueKind != JsonValueKind.Number || !size.TryGetInt32(out pageSize) || pageSize is < 1 or > 100)) return null;
            if (root.TryGetProperty("cursor", out var handle))
            {
                if (handle.ValueKind != JsonValueKind.String) return null;
                cursor = handle.GetString();
                // A malformed opaque handle is a resource-unavailable outcome, after fresh policy.
                if (cursor is null) return null;
            }
            return new ReadRequest(kind, assessment!, report!, pageSize, cursor);
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException or ArgumentException) { return null; }
    }

    private static bool String(JsonElement root, string name, out string? value)
    {
        value = null;
        if (!root.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String) return false;
        value = property.GetString();
        return value is not null;
    }
}
