using System.Text.Json;
using SyntheticEvaluation;

namespace SyntheticEvaluationPopulationIntegration;

internal sealed record PopulationBindingDefinition(SamplingVersionKind Kind, string[] RequiredPaths, string[] KnownPaths, string? MissingReason);
internal sealed record PopulationNativeDocument(string Alias, JsonElement Root);
internal static partial class PopulationInventory
{
    internal static IReadOnlyList<Phase1BPopulationVersionBinding> Build(IReadOnlyList<PopulationNativeDocument> documents, PopulationReferences references)
    {
        var bindings = new List<Phase1BPopulationVersionBinding>();
        foreach (var definition in Definitions)
        {
            var required = Collect(documents, definition.RequiredPaths, true);
            var known = Collect(documents, definition.KnownPaths, false);
            var valueJson = definition.MissingReason is null ? PopulationCanonical.Json(required) : null;
            var knownJson = known.Count == 0 ? null : PopulationCanonical.Json(known);
            bindings.Add(new(definition.Kind, definition.MissingReason is null ? Phase1BPopulationBindingState.SourceBound : Phase1BPopulationBindingState.Missing,
                valueJson is null ? null : references.Add("version-binding", definition.Kind.ToString(), valueJson), valueJson, knownJson,
                required.Keys.Concat(known.Keys).Distinct(StringComparer.Ordinal), definition.MissingReason));
        }
        return bindings;
    }
    private static SortedDictionary<string, JsonElement> Collect(IReadOnlyList<PopulationNativeDocument> documents, IEnumerable<string> paths, bool required)
    {
        var result = new SortedDictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            var split = path.IndexOf(".$.", StringComparison.Ordinal);
            var alias = path[..split]; var segments = path[(split + 3)..].Split('.');
            var matching = documents.Where(d => d.Alias == alias || d.Alias.StartsWith(alias + "[", StringComparison.Ordinal)).ToArray();
            // O/G are optional when the saved source has no findings; I and A are required actual work contexts.
            if (required && matching.Length == 0 && alias is not ("O" or "G")) throw new InvalidOperationException("Missing native source alias.");
            foreach (var document in matching) Expand(document.Root, document.Alias + ".$", segments, 0, required, result);
        }
        return result;
    }
    private static void Expand(JsonElement node, string concrete, string[] segments, int index, bool required, SortedDictionary<string, JsonElement> result)
    {
        if (index == segments.Length)
        {
            if (node.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) { if (required) throw new InvalidOperationException("Null required native source."); return; }
            if (result.TryGetValue(concrete, out var prior) && PopulationCanonical.Encode(prior) != PopulationCanonical.Encode(node)) throw new InvalidOperationException("Conflicting source path.");
            result[concrete] = node.Clone(); return;
        }
        var segment = segments[index];
        var fields = segment.StartsWith('{') && segment.EndsWith('}') ? segment[1..^1].Split(',') : [segment];
        foreach (var field in fields)
        {
            var array = field.EndsWith("[*]", StringComparison.Ordinal); var name = array ? field[..^3] : field;
            if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty(name, out var next) || next.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            { if (required) throw new InvalidOperationException("Missing required native source field."); continue; }
            if (!array) { Expand(next, concrete + "." + name, segments, index + 1, required, result); continue; }
            if (next.ValueKind != JsonValueKind.Array) { if (required) throw new InvalidOperationException("Invalid required native array."); continue; }
            var position = 0;
            foreach (var item in next.EnumerateArray()) Expand(item, concrete + "." + name + "[" + position++ + "]", segments, index + 1, required, result);
            if (required && position == 0 && name is "works") throw new InvalidOperationException("Missing required work context.");
        }
    }
}
