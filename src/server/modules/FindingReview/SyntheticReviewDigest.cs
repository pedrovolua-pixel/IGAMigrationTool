using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FindingReview;

public static class SyntheticReviewDigest
{
    public static string Compute<T>(T content)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, JsonSerializer.SerializeToNode(content));
        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }
    internal static string HashText(string content) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    private static void Write(Utf8JsonWriter writer, JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            writer.WriteStartObject();
            foreach (var pair in obj.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                writer.WritePropertyName(pair.Key);
                Write(writer, pair.Value);
            }
            writer.WriteEndObject();
        }
        else if (node is JsonArray array)
        {
            writer.WriteStartArray();
            foreach (var item in array) Write(writer, item);
            writer.WriteEndArray();
        }
        else if (node is null) writer.WriteNullValue();
        else node.WriteTo(writer);
    }
}
