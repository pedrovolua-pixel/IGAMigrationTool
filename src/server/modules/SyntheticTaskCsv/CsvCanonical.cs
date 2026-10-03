using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SyntheticTaskCsv;

public static class CsvCanonical
{
    internal static readonly UTF8Encoding Utf8 = new(false, true);
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public static byte[] Bytes<T>(T value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, JsonSerializer.SerializeToElement(value, Options), 0);
        return stream.ToArray();
    }
    public static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    public static string Json<T>(T value) => Utf8.GetString(Bytes(value));
    internal static T Parse<T>(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = CsvCodec.MaximumDepth });
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, document.RootElement, 0);
        return JsonSerializer.Deserialize<T>(json, Options) ?? throw new JsonException("Missing CSV record.");
    }
    private static void Write(Utf8JsonWriter writer, JsonElement element, int depth)
    {
        if (depth > CsvCodec.MaximumDepth) throw new JsonException("CSV depth exceeded.");
        if (element.ValueKind == JsonValueKind.Object)
        {
            var properties = element.EnumerateObject().ToArray();
            if (properties.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length) throw new JsonException("Duplicate CSV field.");
            writer.WriteStartObject();
            foreach (var property in properties.OrderBy(item => item.Name, StringComparer.Ordinal))
            { writer.WritePropertyName(property.Name); Write(writer, property.Value, depth + 1); }
            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        { writer.WriteStartArray(); foreach (var item in element.EnumerateArray()) Write(writer, item, depth + 1); writer.WriteEndArray(); }
        else element.WriteTo(writer);
    }
}
