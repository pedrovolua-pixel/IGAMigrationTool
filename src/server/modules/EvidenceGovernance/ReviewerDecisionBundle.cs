using System.Security.Cryptography;
using System.Text.Json;

namespace EvidenceGovernance;

/// <summary>
/// Deterministic, metadata-only reviewer decision. Reviewer identity, role,
/// scope and signing key must be resolved by the trusted caller.
/// </summary>
public static class ReviewerDecisionBundle
{
    private const int MaximumBytes = 4096;
    private static readonly HashSet<string> Fields =
    [
        "schemaVersion", "artifactSha256", "scopeId", "reviewerId",
        "role", "decisionAt", "decision"
    ];

    public static byte[] Create(string artifactSha256, string scopeId, string reviewerId,
        string role, DateTimeOffset decisionAt, ReviewerDecision decision)
    {
        if (!GateCheckBundleRules.IsHex(artifactSha256, 64) ||
            !GateCheckBundleRules.IsCheckId(scopeId) ||
            !GateCheckBundleRules.IsCheckId(reviewerId) ||
            !GateCheckBundleRules.IsCheckId(role) ||
            decisionAt.Offset != TimeSpan.Zero || !Enum.IsDefined(decision))
        {
            throw new ArgumentException("Invalid reviewer decision metadata.");
        }

        _ = GateEvidenceRetention.Calculate(decisionAt);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("artifactSha256", artifactSha256.ToUpperInvariant());
            writer.WriteString("scopeId", scopeId);
            writer.WriteString("reviewerId", reviewerId);
            writer.WriteString("role", role);
            writer.WriteString("decisionAt", decisionAt);
            writer.WriteString("decision", decision == ReviewerDecision.Approve ? "APPROVE" : "REJECT");
            writer.WriteEndObject();
        }

        if (stream.Length > MaximumBytes)
        {
            throw new ArgumentException("Reviewer decision exceeds the size limit.");
        }

        return stream.ToArray();
    }

    public static ReviewerDecisionResult Verify(ReadOnlySpan<byte> bundle,
        string? bundleSha256, ReadOnlySpan<byte> signature, RSA? trustedReviewerKey,
        string? expectedArtifactSha256, string? expectedScopeId,
        string? expectedReviewerId, string? expectedRole,
        DateTimeOffset expectedDecisionAt,
        DateTimeOffset now)
    {
        if (bundle.Length is 0 or > MaximumBytes ||
            !GateCheckBundleRules.IsHex(expectedArtifactSha256, 64) ||
            !GateCheckBundleRules.IsCheckId(expectedScopeId) ||
            !GateCheckBundleRules.IsCheckId(expectedReviewerId) ||
            !GateCheckBundleRules.IsCheckId(expectedRole) ||
            expectedDecisionAt.Offset != TimeSpan.Zero || now.Offset != TimeSpan.Zero)
        {
            return ReviewerDecisionResult.InvalidInput;
        }

        if (ArtifactIntegrityVerifier.Verify(bundle, bundleSha256, signature, trustedReviewerKey)
            != ArtifactIntegrityResult.Valid)
        {
            return ReviewerDecisionResult.Untrusted;
        }

        try
        {
            using var document = JsonDocument.Parse(bundle.ToArray(), new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 2
            });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !HasExactly(root) ||
                !root.GetProperty("schemaVersion").TryGetInt32(out var version) || version != 1 ||
                !TryString(root, "artifactSha256", out var artifactSha256) ||
                !GateCheckBundleRules.IsHex(artifactSha256, 64) ||
                !TryString(root, "scopeId", out var scopeId) || !GateCheckBundleRules.IsCheckId(scopeId) ||
                !TryString(root, "reviewerId", out var reviewerId) || !GateCheckBundleRules.IsCheckId(reviewerId) ||
                !TryString(root, "role", out var role) || !GateCheckBundleRules.IsCheckId(role) ||
                !TryString(root, "decisionAt", out var decisionText) ||
                !(decisionText.EndsWith('Z') || decisionText.EndsWith("+00:00", StringComparison.Ordinal)) ||
                !root.GetProperty("decisionAt").TryGetDateTimeOffset(out var decisionAt) ||
                decisionAt.Offset != TimeSpan.Zero ||
                !TryString(root, "decision", out var decision) ||
                decision is not ("APPROVE" or "REJECT"))
            {
                return ReviewerDecisionResult.InvalidSchema;
            }

            if (!string.Equals(artifactSha256, expectedArtifactSha256, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(scopeId, expectedScopeId, StringComparison.Ordinal) ||
                !string.Equals(reviewerId, expectedReviewerId, StringComparison.Ordinal) ||
                !string.Equals(role, expectedRole, StringComparison.Ordinal) ||
                decisionAt != expectedDecisionAt)
            {
                return ReviewerDecisionResult.WrongScope;
            }

            if (decisionAt > now || GateEvidenceRetention.Calculate(decisionAt).OrdinaryAccessEndsAt <= now)
            {
                return ReviewerDecisionResult.Expired;
            }

            return decision == "APPROVE" ? ReviewerDecisionResult.ValidApproval : ReviewerDecisionResult.Rejected;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
        {
            return ReviewerDecisionResult.InvalidSchema;
        }
    }

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

    private static bool TryString(JsonElement element, string name, out string value)
    {
        var field = element.GetProperty(name);
        value = field.ValueKind == JsonValueKind.String ? field.GetString() ?? "" : "";
        return value.Length > 0;
    }
}

public enum ReviewerDecision
{
    Approve,
    Reject
}

public enum ReviewerDecisionResult
{
    ValidApproval,
    Rejected,
    InvalidInput,
    Untrusted,
    InvalidSchema,
    WrongScope,
    Expired
}
