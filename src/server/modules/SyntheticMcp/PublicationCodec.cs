using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SyntheticMcp;

/// <summary>Validates fictional publication commitments; never promotes a draft or resolves raw evidence.</summary>
public static class PublicationCodec
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly string[] ManifestKeys = ["contractVersion", "fixtureKind", "customerId", "projectId", "environmentId", "assessmentId", "reportVersionId", "baselineVersion", "catalogVersion", "scoringProfileVersion", "maturityProfileVersion", "applicationVersion", "assessmentState", "approvalState", "collections", "items"];
    private static readonly string[] Atoms = ["Fictional finding.", "Fictional summary.", "Fictional coverage.", "Fictional recommendation.", "Fictional option.", "Fictional priority.", "Fictional effort.", "Fictional résumé."];

    public static bool IsId(string? value) => value is { Length: >= 5 and <= 68 } && value.StartsWith("syn-", StringComparison.Ordinal) && value.AsSpan(4).IndexOfAnyExcept("abcdefghijklmnopqrstuvwxyz0123456789-") < 0;

    public static ValidatedManifest? Validate(ManifestSource source, ReadGrant grant)
    {
        try
        {
            if (source is null || grant is null || source.Bytes.IsDefaultOrEmpty || source.Bytes.Length > 1048576 || !IsDigest(source.Digest) || source.Digest != grant.ManifestDigest || !ScopeValid(grant.Scope) || !IsId(grant.AssessmentId) || !IsId(grant.ReportVersionId)) return null;
            if (Digest(source.Bytes.AsSpan()) != source.Digest) return null;
            using var document = Parse(source.Bytes);
            var root = document.RootElement;
            if (!Closed(root, ManifestKeys) || !CanonicalBytes(root).AsSpan().SequenceEqual(source.Bytes.AsSpan())) return null;
            if (Text(root, "contractVersion") != McpContract.Version || Text(root, "fixtureKind") != McpContract.FixtureKind) return null;
            foreach (var key in ManifestKeys[..12].Except(["contractVersion", "fixtureKind"])) if (!IsId(Text(root, key))) return null;
            var scope = new Scope(Text(root, "customerId")!, Text(root, "projectId")!, Text(root, "environmentId")!);
            if (scope != grant.Scope || Text(root, "assessmentId") != grant.AssessmentId || Text(root, "reportVersionId") != grant.ReportVersionId || !OneOf(Text(root, "assessmentState"), "Completed", "CompletedWithGaps") || Text(root, "approvalState") != "SyntheticApproved") return null;
            var collections = root.GetProperty("collections");
            if (collections.ValueKind != JsonValueKind.Array || !collections.EnumerateArray().Select(String).SequenceEqual(Enum.GetNames<ResourceKind>())) return null;
            var items = root.GetProperty("items");
            if (items.ValueKind != JsonValueKind.Array || items.GetArrayLength() > 10000) return null;
            var descriptors = ImmutableArray.CreateBuilder<ItemDescriptor>();
            ItemDescriptor? previous = null;
            foreach (var item in items.EnumerateArray())
            {
                if (!Closed(item, "kind", "itemId", "category", "payloadDigest") || !ExactEnum(Text(item, "kind"), out ResourceKind kind) || !ExactEnum(Text(item, "category"), out EvidenceCategory category) || !IsId(Text(item, "itemId")) || !IsDigest(Text(item, "payloadDigest"))) return null;
                var descriptor = new ItemDescriptor(kind, Text(item, "itemId")!, category, Text(item, "payloadDigest")!);
                if (previous is not null && (kind < previous.Kind || (kind == previous.Kind && StringComparer.Ordinal.Compare(descriptor.ItemId, previous.ItemId) <= 0))) return null;
                descriptors.Add(descriptor); previous = descriptor;
            }
            if (descriptors.Count(x => x.Kind == ResourceKind.PublishedStatus) != 1 || descriptors.Count(x => x.Kind == ResourceKind.Scores) != 1) return null;
            return new(new(McpContract.Version, McpContract.FixtureKind, scope, grant.AssessmentId, grant.ReportVersionId, Text(root, "baselineVersion")!, Text(root, "catalogVersion")!, Text(root, "scoringProfileVersion")!, Text(root, "maturityProfileVersion")!, Text(root, "applicationVersion")!, Text(root, "assessmentState")!, Text(root, "approvalState")!), source.Digest, descriptors.ToImmutable());
        }
        catch (Exception exception) when (Malformed(exception)) { return null; }
    }

    public static JsonElement? ReadPayload(ImmutableArray<byte> bytes, ItemDescriptor descriptor, ValidatedManifest manifest)
    {
        try
        {
            if (bytes.IsDefaultOrEmpty || bytes.Length > 65536 || descriptor is null || manifest is null || !manifest.Items.Contains(descriptor) || !IsDigest(descriptor.PayloadDigest) || Digest(bytes.AsSpan()) != descriptor.PayloadDigest) return null;
            using var document = Parse(bytes);
            var payload = document.RootElement;
            if (!CanonicalBytes(payload).AsSpan().SequenceEqual(bytes.AsSpan()) || !ValidPayload(payload, descriptor, manifest)) return null;
            return payload.Clone();
        }
        catch (Exception exception) when (Malformed(exception)) { return null; }
    }

    internal static bool ValidPayload(JsonElement payload, ItemDescriptor descriptor, ValidatedManifest manifest)
    {
        if (!Closed(payload, Projection.SourceFields(descriptor.Kind).ToArray()) || Text(payload, "itemId") != descriptor.ItemId || Text(payload, "category") != descriptor.Category.ToString()) return false;
        switch (descriptor.Kind)
        {
            case ResourceKind.PublishedStatus:
                return Text(payload, "assessmentState") == manifest.Bindings.AssessmentState && Text(payload, "approvalState") == manifest.Bindings.ApprovalState && Reasons(payload.GetProperty("limitations"));
            case ResourceKind.Coverage:
                return Integer(payload, "assessed", 0, 100000) && Integer(payload, "gap", 0, 100000) && Integer(payload, "unavailable", 0, 100000) && SafeText(Text(payload, "label")) && Reasons(payload.GetProperty("limitations"));
            case ResourceKind.Scores:
                if (!Score(payload, "health") || !Score(payload, "quality") || !(payload.GetProperty("maturity").ValueKind == JsonValueKind.Null || Integer(payload, "maturity", 1, 5))) return false;
                var missing = new[] { "health", "quality", "maturity" }.Any(key => payload.GetProperty(key).ValueKind == JsonValueKind.Null);
                return missing ? Text(payload, "reason") == "Unavailable" : OneOf(Text(payload, "reason"), "None", "DeclaredGap");
            case ResourceKind.Findings:
                return SafeText(Text(payload, "title")) && SafeText(Text(payload, "summary")) && OneOf(Text(payload, "severity"), "Critical", "High", "Medium", "Low", "Informational") && OneOf(Text(payload, "reviewState"), "Proposed", "Confirmed", "Rejected", "Deferred") && OneOf(Text(payload, "confidence"), "High", "Medium", "Low", "Unknown") && payload.GetProperty("mandatoryReview").ValueKind is JsonValueKind.True or JsonValueKind.False && IdLinks(payload.GetProperty("referenceIds"), manifest, ResourceKind.ProtectedReferences);
            case ResourceKind.Recommendations:
                return IsId(Text(payload, "findingId")) && manifest.Items.Any(x => x.Kind == ResourceKind.Findings && x.ItemId == Text(payload, "findingId")) && SafeText(Text(payload, "summary")) && TextArray(payload.GetProperty("options")) && SafeText(Text(payload, "priority")) && SafeText(Text(payload, "effort")) && OneOf(Text(payload, "reviewLabel"), "Unverified", "SyntheticReviewed");
            case ResourceKind.ProtectedReferences:
                return ExactEnum(Text(payload, "availability"), out Availability _) && ExactEnum(Text(payload, "reason"), out SafeReason _);
            default: return false;
        }
    }

    private static bool IdLinks(JsonElement value, ValidatedManifest manifest, ResourceKind kind)
    {
        if (value.ValueKind != JsonValueKind.Array) return false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in value.EnumerateArray())
        {
            var id = String(item);
            if (!IsId(id) || !seen.Add(id!) || !manifest.Items.Any(x => x.Kind == kind && x.ItemId == id)) return false;
        }
        return true;
    }

    private static bool TextArray(JsonElement value) => value.ValueKind == JsonValueKind.Array && value.EnumerateArray().All(x => SafeText(String(x)));
    private static bool Reasons(JsonElement value) => value.ValueKind == JsonValueKind.Array && value.EnumerateArray().All(x => ExactEnum(String(x), out SafeReason _));
    private static bool Score(JsonElement value, string key) { var field = value.GetProperty(key); return field.ValueKind == JsonValueKind.Null || (field.ValueKind == JsonValueKind.Number && field.TryGetDecimal(out var number) && number is >= 0 and <= 100); }
    private static bool Integer(JsonElement value, string key, int minimum, int maximum) { var field = value.GetProperty(key); return field.ValueKind == JsonValueKind.Number && field.TryGetInt32(out var number) && number >= minimum && number <= maximum; }
    private static bool SafeText(string? value)
    {
        if (value is null || value.Length > 4096) return false;
        foreach (var atom in Atoms)
        {
            var remaining = value.AsSpan();
            while (remaining.StartsWith(atom, StringComparison.Ordinal))
            {
                remaining = remaining[atom.Length..];
                if (remaining.IsEmpty) return true;
                if (remaining[0] != ' ') break;
                remaining = remaining[1..];
            }
        }
        return false;
    }

    internal static bool ScopeValid(Scope? scope) => scope is not null && IsId(scope.CustomerId) && IsId(scope.ProjectId) && IsId(scope.EnvironmentId);
    internal static bool ExactEnum<T>(string? text, out T value) where T : struct, Enum => Enum.TryParse(text, false, out value) && Enum.IsDefined(value) && value.ToString() == text;
    internal static bool IsDigest(string? value) => value is { Length: 64 } && value.AsSpan().IndexOfAnyExcept("0123456789abcdef") < 0;
    internal static string Digest(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal static string? Text(JsonElement value, string key) => value.TryGetProperty(key, out var field) ? String(field) : null;
    private static string? String(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static bool OneOf(string? value, params string[] allowed) => value is not null && allowed.Contains(value, StringComparer.Ordinal);
    private static bool Closed(JsonElement value, params string[] keys) => value.ValueKind == JsonValueKind.Object && value.EnumerateObject().Select(x => x.Name).Order(StringComparer.Ordinal).SequenceEqual(keys.Order(StringComparer.Ordinal));
    internal static bool Malformed(Exception exception) => exception is JsonException or ArgumentException or InvalidOperationException or FormatException or OverflowException;
    private static JsonDocument Parse(ImmutableArray<byte> bytes) { _ = StrictUtf8.GetString(bytes.AsSpan()); return JsonDocument.Parse(bytes.AsMemory(), new JsonDocumentOptions { MaxDepth = 16 }); }

    /// <summary>Exact local canonical UTF-8. Invalid JSON values throw; boundary validators translate them to denial.</summary>
    public static byte[] CanonicalBytes(JsonElement value)
    {
        var builder = new StringBuilder(); Write(value, builder, 0); return StrictUtf8.GetBytes(builder.ToString());
    }

    private static void Write(JsonElement value, StringBuilder output, int depth)
    {
        if (depth > 16) throw new JsonException();
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                var properties = value.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal).ToArray();
                if (properties.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length) throw new JsonException();
                output.Append('{');
                for (var index = 0; index < properties.Length; index++) { if (index > 0) output.Append(','); WriteString(properties[index].Name, output); output.Append(':'); Write(properties[index].Value, output, depth + 1); }
                output.Append('}'); break;
            case JsonValueKind.Array:
                output.Append('['); var first = true;
                foreach (var item in value.EnumerateArray()) { if (!first) output.Append(','); first = false; Write(item, output, depth + 1); }
                output.Append(']'); break;
            case JsonValueKind.String: WriteString(value.GetString()!, output); break;
            case JsonValueKind.Number:
                if (!value.TryGetDecimal(out var number)) throw new JsonException();
                output.Append(number.ToString("0.############################", CultureInfo.InvariantCulture)); break;
            case JsonValueKind.True: output.Append("true"); break;
            case JsonValueKind.False: output.Append("false"); break;
            case JsonValueKind.Null: output.Append("null"); break;
            default: throw new JsonException();
        }
    }

    private static void WriteString(string value, StringBuilder output)
    {
        output.Append('"');
        foreach (var character in value)
        {
            switch (character)
            {
                case '"': output.Append("\\\""); break;
                case '\\': output.Append("\\\\"); break;
                case '\b': output.Append("\\b"); break;
                case '\f': output.Append("\\f"); break;
                case '\n': output.Append("\\n"); break;
                case '\r': output.Append("\\r"); break;
                case '\t': output.Append("\\t"); break;
                default: if (character < 32) output.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture)); else output.Append(character); break;
            }
        }
        output.Append('"');
    }
}
