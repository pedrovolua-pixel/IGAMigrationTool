using System.Security.Cryptography;
using System.Text.Json;

namespace EvidenceGovernance;

/// <summary>
/// Verifies a signed, metadata-only CI check bundle. A valid bundle is one input to
/// a gate review; reviewer approval and restricted-store authorization are separate.
/// </summary>
public static class GateCheckBundleVerifier
{
    private static readonly HashSet<string> RootFields =
    [
        "schemaVersion", "gate", "commitSha", "artifactSha256",
        "decisionAt", "checks"
    ];
    private static readonly HashSet<string> CheckFields = ["id", "result"];

    public static GateCheckBundleResult Verify(
        ReadOnlySpan<byte> bundle,
        string? bundleSha256,
        ReadOnlySpan<byte> signature,
        RSA? trustedSigningKey,
        string? expectedGate,
        string? expectedCommitSha,
        string? expectedArtifactSha256,
        IReadOnlyCollection<string>? requiredChecks,
        DateTimeOffset expectedDecisionAt,
        DateTimeOffset now)
    {
        if (bundle.Length is 0 or > GateCheckBundleRules.MaximumBundleBytes ||
            !GateCheckBundleRules.IsGate(expectedGate) || !GateCheckBundleRules.IsHex(expectedCommitSha, 40) ||
            !GateCheckBundleRules.IsHex(expectedArtifactSha256, 64) || requiredChecks is null ||
            requiredChecks.Count == 0 || requiredChecks.Any(id => !GateCheckBundleRules.IsCheckId(id)) ||
            requiredChecks.Count != requiredChecks.Distinct(StringComparer.Ordinal).Count() ||
            expectedDecisionAt.Offset != TimeSpan.Zero || now.Offset != TimeSpan.Zero)
        {
            return GateCheckBundleResult.InvalidInput;
        }

        if (ArtifactIntegrityVerifier.Verify(bundle, bundleSha256, signature, trustedSigningKey)
            != ArtifactIntegrityResult.Valid)
        {
            return GateCheckBundleResult.Untrusted;
        }

        try
        {
            using var document = JsonDocument.Parse(bundle.ToArray(), new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 4
            });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !HasExactly(root, RootFields) ||
                !root.GetProperty("schemaVersion").TryGetInt32(out var version) || version != 1 ||
                !TryString(root, "gate", out var gate) || !GateCheckBundleRules.IsGate(gate) ||
                !TryString(root, "commitSha", out var commitSha) || !GateCheckBundleRules.IsHex(commitSha, 40) ||
                !TryString(root, "artifactSha256", out var artifactSha256) || !GateCheckBundleRules.IsHex(artifactSha256, 64) ||
                !TryString(root, "decisionAt", out var decisionText) ||
                !(decisionText.EndsWith('Z') || decisionText.EndsWith("+00:00", StringComparison.Ordinal)) ||
                !root.GetProperty("decisionAt").TryGetDateTimeOffset(out var decisionAt) ||
                decisionAt.Offset != TimeSpan.Zero ||
                root.GetProperty("checks").ValueKind != JsonValueKind.Array)
            {
                return GateCheckBundleResult.InvalidSchema;
            }

            if (!string.Equals(gate, expectedGate, StringComparison.Ordinal) ||
                !string.Equals(commitSha, expectedCommitSha, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(artifactSha256, expectedArtifactSha256, StringComparison.OrdinalIgnoreCase) ||
                decisionAt != expectedDecisionAt)
            {
                return GateCheckBundleResult.WrongScope;
            }

            if (decisionAt > now || GateEvidenceRetention.Calculate(decisionAt).OrdinaryAccessEndsAt <= now)
            {
                return GateCheckBundleResult.Expired;
            }

            var expected = requiredChecks.ToHashSet(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var check in root.GetProperty("checks").EnumerateArray())
            {
                if (check.ValueKind != JsonValueKind.Object || !HasExactly(check, CheckFields) ||
                    !TryString(check, "id", out var id) || !GateCheckBundleRules.IsCheckId(id) ||
                    !TryString(check, "result", out var result) ||
                    result is not ("PASS" or "FAIL" or "NOT_VERIFIED") ||
                    !expected.Contains(id) || !seen.Add(id))
                {
                    return GateCheckBundleResult.InvalidSchema;
                }

                if (result != "PASS")
                {
                    return GateCheckBundleResult.ChecksNotPassed;
                }
            }

            return seen.SetEquals(expected)
                ? GateCheckBundleResult.Valid
                : GateCheckBundleResult.ChecksNotPassed;
        }
        catch (JsonException)
        {
            return GateCheckBundleResult.InvalidSchema;
        }
        catch (InvalidOperationException)
        {
            return GateCheckBundleResult.InvalidSchema;
        }
        catch (ArgumentOutOfRangeException)
        {
            return GateCheckBundleResult.InvalidSchema;
        }
        catch (ArgumentException)
        {
            return GateCheckBundleResult.InvalidSchema;
        }
    }

    private static bool HasExactly(JsonElement element, HashSet<string> expected)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!expected.Contains(property.Name) || !seen.Add(property.Name))
            {
                return false;
            }
        }

        return seen.SetEquals(expected);
    }

    private static bool TryString(JsonElement element, string name, out string value)
    {
        var field = element.GetProperty(name);
        value = field.ValueKind == JsonValueKind.String ? field.GetString() ?? "" : "";
        return value.Length > 0;
    }

}

public enum GateCheckBundleResult
{
    Valid,
    InvalidInput,
    Untrusted,
    InvalidSchema,
    WrongScope,
    Expired,
    ChecksNotPassed
}
