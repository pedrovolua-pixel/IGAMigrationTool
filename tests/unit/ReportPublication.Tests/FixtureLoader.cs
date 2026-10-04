using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;
using ReportPublication;

// Test-only constructor mapping of independently validated original fixture bytes.
// This is not a production parser and does not accept real source JSON.
internal static class FixtureLoader
{
    internal static T Load<T>(JsonNode node) => (T)Load(typeof(T), node)!;
    private static object? Load(Type type, JsonNode? node)
    {
        if (node is null) return null;
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null) return Load(underlying, node);
        if (type == typeof(string)) return node.GetValue<string>();
        if (type == typeof(Guid)) return Guid.Parse(node.GetValue<string>());
        if (type == typeof(long)) return long.Parse(node.GetValue<string>(), CultureInfo.InvariantCulture);
        if (type == typeof(decimal)) return decimal.Parse(node.GetValue<string>(), CultureInfo.InvariantCulture);
        if (type == typeof(bool)) return node.GetValue<bool>();
        if (type == typeof(DateTimeOffset)) return DateTimeOffset.Parse(node.GetValue<string>(), CultureInfo.InvariantCulture);
        if (type.IsEnum) return Enum.Parse(type, node.GetValue<string>());
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            var element = type.GetGenericArguments()[0];
            var array = Array.CreateInstance(element, node.AsArray().Count);
            for (var i = 0; i < array.Length; i++) array.SetValue(Load(element, node[i]), i);
            return array;
        }
        var constructor = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Single(c => c.GetParameters().All(v => v.ParameterType != type));
        return constructor.Invoke(constructor.GetParameters().Select(parameter =>
        {
            var key = char.ToLowerInvariant(parameter.Name![0]) + parameter.Name[1..];
            var value = node[key];
            if ((type == typeof(PublicationProjectionV1) || type == typeof(PublicationManifestV1)) && parameter.Name == "scope")
            {
                value = value!.DeepClone(); value["assessmentId"] = node["assessmentId"]!.DeepClone();
            }
            return Load(parameter.ParameterType, value);
        }).ToArray());
    }
    internal static SourceCaptureV1 Source(JsonNode node) => new(Load<PublicationProjectionV1>(node["projection"]!),
        Load<AssessmentStateV1>(node["assessmentState"]!), Load<PublicationRetentionV1>(node["retention"]!),
        node["requiredCategories"]!.AsArray().Select(v => v!.GetValue<string>()),
        node["requiredFields"]!.AsArray().Select(v => v!.GetValue<string>()),
        node["provenance"]!.AsArray().Select(v => Load<PublicationProvenanceV1>(v!)));
}
