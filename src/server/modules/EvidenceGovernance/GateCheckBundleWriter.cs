using System.Text.Json;

namespace EvidenceGovernance;

/// <summary>
/// Creates a deterministic, metadata-only bundle from a trusted gate configuration.
/// It does not sign, store, approve, or promote the bundle.
/// </summary>
public static class GateCheckBundleWriter
{
    public static byte[] Create(
        string gate,
        string commitSha,
        string artifactSha256,
        DateTimeOffset decisionAt,
        IReadOnlyCollection<string> requiredChecks,
        IReadOnlyDictionary<string, CheckOutcome> reportedChecks)
    {
        if (!GateCheckBundleRules.IsGate(gate) ||
            !GateCheckBundleRules.IsHex(commitSha, 40) ||
            !GateCheckBundleRules.IsHex(artifactSha256, 64) ||
            decisionAt.Offset != TimeSpan.Zero ||
            requiredChecks is null || requiredChecks.Count is 0 or > 512 ||
            reportedChecks is null ||
            requiredChecks.Any(id => !GateCheckBundleRules.IsCheckId(id)) ||
            requiredChecks.Count != requiredChecks.Distinct(StringComparer.Ordinal).Count())
        {
            throw new ArgumentException("Invalid gate-check bundle metadata.");
        }

        _ = GateEvidenceRetention.Calculate(decisionAt);

        var required = requiredChecks.ToHashSet(StringComparer.Ordinal);
        if (reportedChecks.Keys.Any(id => !required.Contains(id)) ||
            reportedChecks.Values.Any(outcome => !Enum.IsDefined(outcome)))
        {
            throw new ArgumentException("Invalid gate-check result set.");
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("gate", gate);
            writer.WriteString("commitSha", commitSha.ToUpperInvariant());
            writer.WriteString("artifactSha256", artifactSha256.ToUpperInvariant());
            writer.WriteString("decisionAt", decisionAt);
            writer.WriteStartArray("checks");
            foreach (var id in required.OrderBy(id => id, StringComparer.Ordinal))
            {
                var outcome = reportedChecks.TryGetValue(id, out var reported)
                    ? reported
                    : CheckOutcome.NotVerified;
                writer.WriteStartObject();
                writer.WriteString("id", id);
                writer.WriteString("result", outcome switch
                {
                    CheckOutcome.Pass => "PASS",
                    CheckOutcome.Fail => "FAIL",
                    _ => "NOT_VERIFIED"
                });
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        if (stream.Length > GateCheckBundleRules.MaximumBundleBytes)
        {
            throw new ArgumentException("Gate-check bundle exceeds the size limit.");
        }

        return stream.ToArray();
    }
}

public enum CheckOutcome
{
    Pass,
    Fail,
    NotVerified
}
