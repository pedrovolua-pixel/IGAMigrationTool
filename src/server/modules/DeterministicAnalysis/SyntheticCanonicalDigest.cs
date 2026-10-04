using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DeterministicAnalysis;

/// <summary>Internal v1 canonical JSON: ordinal object properties, significant array order, invariant JSON numbers.</summary>
public static class SyntheticCanonicalDigest
{
    public static string Compute<T>(T content)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            Write(writer, JsonSerializer.SerializeToNode(content));
        }
        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }

    private static void Write(Utf8JsonWriter writer, JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            writer.WriteStartObject();
            foreach (var property in obj.OrderBy(property => property.Key, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Key);
                Write(writer, property.Value);
            }
            writer.WriteEndObject();
        }
        else if (node is JsonArray array)
        {
            writer.WriteStartArray();
            foreach (var value in array) Write(writer, value);
            writer.WriteEndArray();
        }
        else if (node is null) writer.WriteNullValue();
        else node.WriteTo(writer);
    }
}
