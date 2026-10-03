using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace SyntheticEvaluationSourceIntegration;

internal static class SourceCanonical
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter(allowIntegerValues: false) } };
    internal static string Json<T>(T value) => Encode(JsonSerializer.SerializeToElement(value, Options));
    // Reproduce existing SyntheticCanonicalDigest bytes without changing its owner representation.
    internal static string GeneratedJson<T>(T value) => Encode(JsonSerializer.SerializeToElement(value));
    internal static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string Encode(JsonElement value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, value);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
    private static void Write(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
            { writer.WritePropertyName(property.Name); Write(writer, property.Value); }
            writer.WriteEndObject();
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in value.EnumerateArray()) Write(writer, item);
            writer.WriteEndArray();
        }
        else value.WriteTo(writer);
    }
}
