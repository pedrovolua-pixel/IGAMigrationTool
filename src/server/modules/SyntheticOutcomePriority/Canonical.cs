using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SyntheticOutcomePriority;

public static class OutcomePriorityCanonical
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false), new DefaultImmutableArrayConverterFactory() }
    };
    public static string Json<T>(T value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, JsonSerializer.SerializeToElement(value, Options));
        return Encoding.UTF8.GetString(stream.ToArray());
    }
    public static T Parse<T>(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
        CheckDuplicates(doc.RootElement);
        return JsonSerializer.Deserialize<T>(json, Options) ?? throw new JsonException("Missing outcome record.");
    }
    public static string Digest<T>(T value) => Hash(Json(value));
    public static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static OutcomeContentVersion Seal(OutcomeContentVersion content) => content with
    {
        UnitLinks = content.UnitLinks.OrderBy(k => k.InventoryId, StringComparer.Ordinal).ThenBy(k => k.EvidenceCategory, StringComparer.Ordinal).ToImmutableArray(),
        ReferenceIds = content.ReferenceIds.Order(StringComparer.Ordinal).ToImmutableArray(),
        ContentDigest = Digest(content with { ContentDigest = "", UnitLinks = content.UnitLinks.OrderBy(k => k.InventoryId, StringComparer.Ordinal).ThenBy(k => k.EvidenceCategory, StringComparer.Ordinal).ToImmutableArray(), ReferenceIds = content.ReferenceIds.Order(StringComparer.Ordinal).ToImmutableArray() })
    };
    public static Phase1BPlanningSource Seal(Phase1BPlanningSource source)
    {
        var sorted = source with { SourceDigest = "", Options = source.Options.OrderBy(o => o.OptionId, StringComparer.Ordinal).Select(o => o with { Factors = o.Factors with
        {
            AffectedObjectIds = o.Factors.AffectedObjectIds.IsDefault ? default : o.Factors.AffectedObjectIds.Order(StringComparer.Ordinal).ToImmutableArray(),
            Objectives = o.Factors.Objectives.IsDefault ? default : o.Factors.Objectives.OrderBy(v => v.ObjectiveId, StringComparer.Ordinal).ToImmutableArray(),
            MatchedObjectiveIds = o.Factors.MatchedObjectiveIds.IsDefault ? default : o.Factors.MatchedObjectiveIds.Order(StringComparer.Ordinal).ToImmutableArray()
        } }).ToImmutableArray() };
        return sorted with { SourceDigest = Digest(sorted) };
    }
    private static void CheckDuplicates(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var props = value.EnumerateObject().ToArray();
            if (props.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != props.Length) throw new JsonException("Duplicate outcome property.");
            foreach (var p in props) CheckDuplicates(p.Value);
        }
        else if (value.ValueKind == JsonValueKind.Array) foreach (var child in value.EnumerateArray()) CheckDuplicates(child);
    }
    private static void Write(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject(); foreach (var p in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)) { writer.WritePropertyName(p.Name); Write(writer, p.Value); } writer.WriteEndObject(); break;
            case JsonValueKind.Array:
                writer.WriteStartArray(); foreach (var child in value.EnumerateArray()) Write(writer, child); writer.WriteEndArray(); break;
            case JsonValueKind.Number:
                if (value.TryGetDecimal(out var number)) writer.WriteRawValue(number.ToString("G29", CultureInfo.InvariantCulture)); else throw new JsonException("Nondecimal outcome number."); break;
            default: value.WriteTo(writer); break;
        }
    }
}

internal sealed class DefaultImmutableArrayConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableArray<>);
    public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options) => (JsonConverter)Activator.CreateInstance(typeof(DefaultImmutableArrayConverter<>).MakeGenericType(type.GetGenericArguments()[0]))!;
}
internal sealed class DefaultImmutableArrayConverter<T> : JsonConverter<ImmutableArray<T>>
{
    public override ImmutableArray<T> Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => reader.TokenType == JsonTokenType.Null ? default : JsonSerializer.Deserialize<T[]>(ref reader, options)!.ToImmutableArray();
    public override void Write(Utf8JsonWriter writer, ImmutableArray<T> value, JsonSerializerOptions options)
    {
        if (value.IsDefault) { writer.WriteNullValue(); return; }
        writer.WriteStartArray(); foreach (var item in value) JsonSerializer.Serialize(writer, item, options); writer.WriteEndArray();
    }
}
