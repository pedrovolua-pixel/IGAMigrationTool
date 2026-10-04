using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SyntheticFixReview;

internal static class ArtifactReviewCanonical
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };
    internal static string Json<T>(T value) => Encoding.UTF8.GetString(Bytes(JsonSerializer.SerializeToElement(value, Options)));
    internal static string Digest<T>(T value) => Hash(Json(value));
    internal static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    internal static T Parse<T>(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });
        _ = Bytes(document.RootElement);
        return JsonSerializer.Deserialize<T>(json, Options) ?? throw new JsonException("Missing review record.");
    }
    internal static byte[] Bytes(JsonElement value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, value);
        return stream.ToArray();
    }
    private static void Write(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                var properties = value.EnumerateObject().ToArray();
                if (properties.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length) throw new JsonException("Duplicate review field.");
                writer.WriteStartObject();
                foreach (var item in properties.OrderBy(item => item.Name, StringComparer.Ordinal)) { writer.WritePropertyName(item.Name); Write(writer, item.Value); }
                writer.WriteEndObject(); break;
            case JsonValueKind.Array:
                writer.WriteStartArray(); foreach (var item in value.EnumerateArray()) Write(writer, item); writer.WriteEndArray(); break;
            default: value.WriteTo(writer); break;
        }
    }
    internal static string CommandDigest(ArtifactReviewScope scope, Guid runId, string artifactId, string actorId, ArtifactReviewCommand command) =>
        Digest(new { schemaVersion = "synthetic-fix-review-command-v1", scope, runId, artifactId, actorId, command });
}
