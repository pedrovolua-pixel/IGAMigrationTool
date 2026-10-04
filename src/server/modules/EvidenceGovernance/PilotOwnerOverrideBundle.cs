using System.Security.Cryptography;
using System.Text.Json;

namespace EvidenceGovernance;

/// <summary>
/// Checks one signed pilot-owner exception for exact artifact, scope, target and
/// waived checks. A valid bundle is evidence for an operator, not activation.
/// </summary>
public static class PilotOwnerOverrideBundle
{
    private const int MaximumBytes = 8192;
    private static readonly HashSet<string> Fields =
    [
        "schemaVersion", "artifactSha256", "scopeId", "ownerId", "customerId",
        "environmentId", "capabilityId", "waivedChecks", "rationaleSha256",
        "suspensionTargetId", "decisionAt", "expiresAt"
    ];

    public static byte[] Create(OwnerOverrideMetadata metadata, ReadOnlySpan<byte> rationale)
    {
        if (!IsValidMetadata(metadata) || rationale.IsEmpty)
        {
            throw new ArgumentException("Invalid pilot owner override metadata.");
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("artifactSha256", metadata.ArtifactSha256.ToUpperInvariant());
            writer.WriteString("scopeId", metadata.ScopeId);
            writer.WriteString("ownerId", metadata.OwnerId);
            writer.WriteString("customerId", metadata.CustomerId);
            writer.WriteString("environmentId", metadata.EnvironmentId);
            writer.WriteString("capabilityId", metadata.CapabilityId);
            writer.WriteStartArray("waivedChecks");
            foreach (var check in metadata.WaivedChecks)
            {
                writer.WriteStringValue(check);
            }

            writer.WriteEndArray();
            writer.WriteString("rationaleSha256", Convert.ToHexString(SHA256.HashData(rationale)));
            writer.WriteString("suspensionTargetId", metadata.SuspensionTargetId);
            writer.WriteString("decisionAt", metadata.DecisionAt);
            writer.WriteString("expiresAt", metadata.ExpiresAt);
            writer.WriteEndObject();
        }

        if (stream.Length > MaximumBytes)
        {
            throw new ArgumentException("Pilot owner override exceeds the size limit.");
        }

        return stream.ToArray();
    }

    public static OwnerOverrideResult Verify(ReadOnlySpan<byte> bundle, string? bundleSha256,
        ReadOnlySpan<byte> signature, RSA? trustedOwnerKey, ReadOnlySpan<byte> rationale,
        OwnerOverrideMetadata? expected, DateTimeOffset trustedPilotEndsAt,
        DateTimeOffset now)
    {
        if (bundle.Length is 0 or > MaximumBytes || rationale.IsEmpty ||
            !IsValidMetadata(expected) || trustedPilotEndsAt.Offset != TimeSpan.Zero ||
            now.Offset != TimeSpan.Zero)
        {
            return OwnerOverrideResult.InvalidInput;
        }

        if (ArtifactIntegrityVerifier.Verify(bundle, bundleSha256, signature, trustedOwnerKey)
            != ArtifactIntegrityResult.Valid)
        {
            return OwnerOverrideResult.Untrusted;
        }

        try
        {
            using var document = JsonDocument.Parse(bundle.ToArray(), new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 3
            });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !HasExactly(root) ||
                !root.GetProperty("schemaVersion").TryGetInt32(out var version) || version != 1 ||
                !TryString(root, "artifactSha256", out var artifactSha256) ||
                !GateCheckBundleRules.IsHex(artifactSha256, 64) ||
                !TryString(root, "scopeId", out var scopeId) || !GateCheckBundleRules.IsCheckId(scopeId) ||
                !TryString(root, "ownerId", out var ownerId) || !GateCheckBundleRules.IsCheckId(ownerId) ||
                !TryString(root, "customerId", out var customerId) || !GateCheckBundleRules.IsCheckId(customerId) ||
                !TryString(root, "environmentId", out var environmentId) ||
                !GateCheckBundleRules.IsCheckId(environmentId) ||
                !TryString(root, "capabilityId", out var capabilityId) ||
                !GateCheckBundleRules.IsCheckId(capabilityId) ||
                !TryString(root, "rationaleSha256", out var rationaleSha256) ||
                !GateCheckBundleRules.IsHex(rationaleSha256, 64) ||
                !TryString(root, "suspensionTargetId", out var suspensionTargetId) ||
                !GateCheckBundleRules.IsCheckId(suspensionTargetId) ||
                !TryUtc(root, "decisionAt", out var decisionAt) ||
                !TryUtc(root, "expiresAt", out var expiresAt) ||
                !TryChecks(root, out var waivedChecks))
            {
                return OwnerOverrideResult.InvalidSchema;
            }

            if (!string.Equals(artifactSha256, expected!.ArtifactSha256, StringComparison.OrdinalIgnoreCase) ||
                scopeId != expected.ScopeId || ownerId != expected.OwnerId ||
                customerId != expected.CustomerId || environmentId != expected.EnvironmentId ||
                capabilityId != expected.CapabilityId ||
                !waivedChecks.SequenceEqual(expected.WaivedChecks, StringComparer.Ordinal) ||
                suspensionTargetId != expected.SuspensionTargetId ||
                decisionAt != expected.DecisionAt || expiresAt != expected.ExpiresAt ||
                !string.Equals(rationaleSha256,
                    Convert.ToHexString(SHA256.HashData(rationale)), StringComparison.OrdinalIgnoreCase))
            {
                return OwnerOverrideResult.WrongScope;
            }

            if (decisionAt > now || now >= expiresAt || expiresAt > trustedPilotEndsAt)
            {
                return OwnerOverrideResult.Expired;
            }

            return OwnerOverrideResult.ValidForOperatorReview;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
        {
            return OwnerOverrideResult.InvalidSchema;
        }
    }

    private static bool IsValidMetadata(OwnerOverrideMetadata? item) => item is not null &&
        GateCheckBundleRules.IsHex(item.ArtifactSha256, 64) &&
        GateCheckBundleRules.IsCheckId(item.ScopeId) &&
        GateCheckBundleRules.IsCheckId(item.OwnerId) &&
        GateCheckBundleRules.IsCheckId(item.CustomerId) &&
        GateCheckBundleRules.IsCheckId(item.EnvironmentId) &&
        GateCheckBundleRules.IsCheckId(item.CapabilityId) &&
        GateCheckBundleRules.IsCheckId(item.SuspensionTargetId) &&
        item.WaivedChecks is { Count: > 0 and <= 32 } &&
        item.WaivedChecks.All(GateCheckBundleRules.IsCheckId) &&
        item.WaivedChecks.SequenceEqual(item.WaivedChecks.Order(StringComparer.Ordinal), StringComparer.Ordinal) &&
        item.WaivedChecks.Distinct(StringComparer.Ordinal).Count() == item.WaivedChecks.Count &&
        item.DecisionAt.Offset == TimeSpan.Zero && item.ExpiresAt.Offset == TimeSpan.Zero &&
        item.DecisionAt < item.ExpiresAt;

    private static bool HasExactly(JsonElement element)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!Fields.Contains(property.Name) || !seen.Add(property.Name))
            {
                return false;
            }
        }

        return seen.SetEquals(Fields);
    }

    private static bool TryString(JsonElement root, string field, out string value)
    {
        var element = root.GetProperty(field);
        value = element.ValueKind == JsonValueKind.String ? element.GetString() ?? "" : "";
        return value.Length > 0;
    }

    private static bool TryUtc(JsonElement root, string field, out DateTimeOffset value)
    {
        var element = root.GetProperty(field);
        value = default;
        if (element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var text = element.GetString();
        return text is not null &&
               (text.EndsWith('Z') || text.EndsWith("+00:00", StringComparison.Ordinal)) &&
               element.TryGetDateTimeOffset(out value) && value.Offset == TimeSpan.Zero;
    }

    private static bool TryChecks(JsonElement root, out IReadOnlyList<string> checks)
    {
        checks = [];
        var element = root.GetProperty("waivedChecks");
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() is < 1 or > 32)
        {
            return false;
        }

        var values = new List<string>();
        foreach (var entry in element.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.String ||
                !GateCheckBundleRules.IsCheckId(entry.GetString()))
            {
                return false;
            }

            values.Add(entry.GetString()!);
        }

        if (!values.SequenceEqual(values.Order(StringComparer.Ordinal), StringComparer.Ordinal) ||
            values.Distinct(StringComparer.Ordinal).Count() != values.Count)
        {
            return false;
        }

        checks = values;
        return true;
    }
}

public sealed record OwnerOverrideMetadata(string ArtifactSha256, string ScopeId, string OwnerId,
    string CustomerId, string EnvironmentId, string CapabilityId,
    IReadOnlyList<string> WaivedChecks, string SuspensionTargetId,
    DateTimeOffset DecisionAt, DateTimeOffset ExpiresAt);

public enum OwnerOverrideResult
{
    ValidForOperatorReview,
    InvalidInput,
    Untrusted,
    InvalidSchema,
    WrongScope,
    Expired
}
