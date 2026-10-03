using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SyntheticAiExecution;

public static class AiExecutionCanonical
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };
    public static string Serialize<T>(T value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, JsonSerializer.SerializeToElement(value, Json));
        return Encoding.UTF8.GetString(stream.ToArray());
    }
    public static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string Digest<T>(T value) => Hash(Serialize(value));
    internal static T Parse<T>(string value) => JsonSerializer.Deserialize<T>(value, Json) ?? throw new InvalidOperationException("AI integrity denied.");
    private static void Write(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var field in value.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal)) { writer.WritePropertyName(field.Name); Write(writer, field.Value); }
            writer.WriteEndObject();
        }
        else if (value.ValueKind == JsonValueKind.Array)
        { writer.WriteStartArray(); foreach (var item in value.EnumerateArray()) Write(writer, item); writer.WriteEndArray(); }
        else if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number)) writer.WriteRawValue(number.ToString("G29", System.Globalization.CultureInfo.InvariantCulture));
        else value.WriteTo(writer);
    }
}
