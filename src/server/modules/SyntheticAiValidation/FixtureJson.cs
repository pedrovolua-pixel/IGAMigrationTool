using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SyntheticAiValidation;

internal static class FixtureJson
{
    internal static bool TryParse(string? text, int byteLimit, out JsonDocument? document)
    {
        document = null;
        if (text is null) return false;
        try
        {
            if (new UTF8Encoding(false, true).GetByteCount(text) > byteLimit) return false;
            document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 16 });
            if (!ClosedStringsAndUniqueKeys(document.RootElement))
            {
                document.Dispose();
                document = null;
                return false;
            }
            return true;
        }
        catch (Exception exception) when (exception is JsonException or EncoderFallbackException or InvalidOperationException)
        {
            document?.Dispose();
            document = null;
            return false;
        }
    }

    internal static bool HasOnly(JsonElement value, params string[] keys) =>
        value.ValueKind == JsonValueKind.Object && value.EnumerateObject().Count() == keys.Length &&
        value.EnumerateObject().All(property => keys.Contains(property.Name, StringComparer.Ordinal));

    internal static bool IsDigest(string? text) => text is { Length: 64 } &&
        text.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    internal static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    internal static string Canonical(JsonElement value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, value);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void Write(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    Write(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray()) Write(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String: writer.WriteStringValue(value.GetString()); break;
            case JsonValueKind.Number:
                writer.WriteRawValue(value.GetDecimal().ToString("G29", CultureInfo.InvariantCulture));
                break;
            case JsonValueKind.True: writer.WriteBooleanValue(true); break;
            case JsonValueKind.False: writer.WriteBooleanValue(false); break;
            case JsonValueKind.Null: writer.WriteNullValue(); break;
            default: throw new InvalidOperationException("Unsupported fixture JSON kind.");
        }
    }

    private static bool ClosedStringsAndUniqueKeys(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                var names = new HashSet<string>(StringComparer.Ordinal);
                return value.EnumerateObject().All(property => ValidString(property.Name) &&
                    names.Add(property.Name) && ClosedStringsAndUniqueKeys(property.Value));
            case JsonValueKind.Array: return value.EnumerateArray().All(ClosedStringsAndUniqueKeys);
            case JsonValueKind.String: return ValidString(value.GetString()!);
            default: return true;
        }
    }

    private static bool ValidString(string value)
    {
        if (value.Length > 4096) return false;
        for (var index = 0; index < value.Length; index++)
        {
            if (char.IsHighSurrogate(value[index]))
            {
                if (++index >= value.Length || !char.IsLowSurrogate(value[index])) return false;
            }
            else if (char.IsLowSurrogate(value[index])) return false;
        }
        return true;
    }
}
