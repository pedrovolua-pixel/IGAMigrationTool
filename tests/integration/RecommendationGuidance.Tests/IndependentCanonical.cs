using System.Security.Cryptography;
using System.Text.Json;

internal static class IndependentCanonical
{
    internal static byte[] Bytes(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, element);
        return stream.ToArray();
    }
    private static void Write(Utf8JsonWriter writer, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
            { writer.WritePropertyName(property.Name); Write(writer, property.Value); }
            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        { writer.WriteStartArray(); foreach (var item in element.EnumerateArray()) Write(writer, item); writer.WriteEndArray(); }
        else element.WriteTo(writer);
    }
    internal static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
