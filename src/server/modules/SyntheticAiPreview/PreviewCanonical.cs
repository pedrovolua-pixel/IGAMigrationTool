using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SyntheticAiPreview;

internal static class PreviewCanonical
{
    internal const int MaximumBytes = 4 * 1024 * 1024;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    internal static string Payload(PreviewSource source, string packetDigest, string proposalDigest,
        ImmutableArray<PreviewProposal> proposals)
    {
        var value = JsonSerializer.SerializeToElement(new
        {
            schemaVersion = "synthetic-ai-preview-v1",
            status = "Proposed",
            source,
            packetDigest,
            proposalDigest,
            proposals,
            disclaimer = SyntheticAiPreviewSnapshot.FixedDisclaimer
        }, Options);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, value);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    internal static string Payload(SyntheticAiPreviewSnapshot snapshot) =>
        Payload(snapshot.Source, snapshot.PacketDigest, snapshot.ProposalDigest, snapshot.Proposals);

    internal static string Digest(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static void Write(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
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
            default:
                value.WriteTo(writer);
                break;
        }
    }
}
