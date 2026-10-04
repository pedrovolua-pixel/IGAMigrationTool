using System.Security.Cryptography;
using System.Text.Json;

// Small input-only identity serializer; the full snapshot/string oracle is the independently authored Python literal golden.
internal static class IndependentCanonical
{
    internal static byte[] Bytes(JsonElement value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, value);
        return stream.ToArray();
    }
    private static void Write(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var field in value.EnumerateObject().OrderBy(field => field.Name, StringComparer.Ordinal))
            { writer.WritePropertyName(field.Name); Write(writer, field.Value); }
            writer.WriteEndObject();
        }
        else if (value.ValueKind == JsonValueKind.Array)
        { writer.WriteStartArray(); foreach (var item in value.EnumerateArray()) Write(writer, item); writer.WriteEndArray(); }
        else value.WriteTo(writer);
    }
    internal static string Hash(object input) => Convert.ToHexStringLower(SHA256.HashData(Bytes(JsonSerializer.SerializeToElement(input, new JsonSerializerOptions(JsonSerializerDefaults.Web)))));
}
