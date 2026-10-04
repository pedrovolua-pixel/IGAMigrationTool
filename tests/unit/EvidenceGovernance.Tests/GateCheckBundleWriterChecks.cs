using System.Security.Cryptography;
using System.Text.Json;
using EvidenceGovernance;

internal static class GateCheckBundleWriterChecks
{
    private const string Commit = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Artifact = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private static readonly DateTimeOffset DecisionAt = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

    public static int Run()
    {
        var count = 0;
        var complete = GateCheckBundleWriter.Create("G1", Commit, Artifact, DecisionAt,
            ["SOURCE-SAFETY", "BUILD"], new Dictionary<string, CheckOutcome>
            {
                ["SOURCE-SAFETY"] = CheckOutcome.Pass,
                ["BUILD"] = CheckOutcome.Pass
            });
        var reordered = GateCheckBundleWriter.Create("G1", Commit.ToUpperInvariant(), Artifact.ToUpperInvariant(), DecisionAt,
            ["BUILD", "SOURCE-SAFETY"], new Dictionary<string, CheckOutcome>
            {
                ["BUILD"] = CheckOutcome.Pass,
                ["SOURCE-SAFETY"] = CheckOutcome.Pass
            });
        Expect("deterministic bytes", complete.SequenceEqual(reordered), ref count);

        using var signer = RSA.Create(2048);
        using var trustedKey = RSA.Create();
        trustedKey.ImportParameters(signer.ExportParameters(false));
        Expect("writer output passes signed verification",
            Verify(complete, signer, trustedKey) == GateCheckBundleResult.Valid, ref count);

        var missing = GateCheckBundleWriter.Create("G1", Commit, Artifact, DecisionAt,
            ["BUILD", "SOURCE-SAFETY"], new Dictionary<string, CheckOutcome> { ["BUILD"] = CheckOutcome.Pass });
        using (var document = JsonDocument.Parse(missing))
        {
            var checks = document.RootElement.GetProperty("checks").EnumerateArray().ToArray();
            Expect("missing check is explicit", checks[1].GetProperty("result").GetString() == "NOT_VERIFIED", ref count);
        }

        Expect("missing check cannot verify as passing",
            Verify(missing, signer, trustedKey) == GateCheckBundleResult.ChecksNotPassed, ref count);
        var failed = GateCheckBundleWriter.Create("G1", Commit, Artifact, DecisionAt,
            ["BUILD"], new Dictionary<string, CheckOutcome> { ["BUILD"] = CheckOutcome.Fail });
        Expect("failed check remains failed", Verify(failed, signer, trustedKey, ["BUILD"])
            == GateCheckBundleResult.ChecksNotPassed, ref count);

        Reject("invalid gate", () => GateCheckBundleWriter.Create("G0", Commit, Artifact, DecisionAt,
            ["BUILD"], new Dictionary<string, CheckOutcome>()), ref count);
        Reject("invalid artifact digest", () => GateCheckBundleWriter.Create("G1", Commit, "bad", DecisionAt,
            ["BUILD"], new Dictionary<string, CheckOutcome>()), ref count);
        Reject("duplicate required checks", () => GateCheckBundleWriter.Create("G1", Commit, Artifact, DecisionAt,
            ["BUILD", "BUILD"], new Dictionary<string, CheckOutcome>()), ref count);
        Reject("untrusted reported check", () => GateCheckBundleWriter.Create("G1", Commit, Artifact, DecisionAt,
            ["BUILD"], new Dictionary<string, CheckOutcome> { ["CUSTOMER-X"] = CheckOutcome.Pass }), ref count);
        Reject("invalid result enum", () => GateCheckBundleWriter.Create("G1", Commit, Artifact, DecisionAt,
            ["BUILD"], new Dictionary<string, CheckOutcome> { ["BUILD"] = (CheckOutcome)99 }), ref count);
        Reject("non-UTC decision", () => GateCheckBundleWriter.Create("G1", Commit, Artifact,
            DecisionAt.ToOffset(TimeSpan.FromHours(1)), ["BUILD"], new Dictionary<string, CheckOutcome>()), ref count);
        Reject("unrepresentable retention", () => GateCheckBundleWriter.Create("G1", Commit, Artifact,
            DateTimeOffset.MaxValue, ["BUILD"], new Dictionary<string, CheckOutcome>()), ref count);
        Reject("empty required check set", () => GateCheckBundleWriter.Create("G1", Commit, Artifact,
            DecisionAt, [], new Dictionary<string, CheckOutcome>()), ref count);
        Reject("oversized required check set", () => GateCheckBundleWriter.Create("G1", Commit, Artifact,
            DecisionAt, Enumerable.Range(0, 513).Select(i => $"CHECK-{i}").ToArray(),
            new Dictionary<string, CheckOutcome>()), ref count);
        Reject("free-text check identifier", () => GateCheckBundleWriter.Create("G1", Commit, Artifact,
            DecisionAt, ["customer payload"], new Dictionary<string, CheckOutcome>()), ref count);

        return count;
    }

    private static GateCheckBundleResult Verify(byte[] bundle, RSA signer, RSA trustedKey,
        IReadOnlyCollection<string>? required = null)
    {
        return GateCheckBundleVerifier.Verify(bundle, Convert.ToHexString(SHA256.HashData(bundle)),
            signer.SignData(bundle, HashAlgorithmName.SHA256, RSASignaturePadding.Pss), trustedKey,
            "G1", Commit, Artifact, required ?? ["BUILD", "SOURCE-SAFETY"], DecisionAt, DecisionAt.AddHours(1));
    }

    private static void Expect(string name, bool condition, ref int count)
    {
        if (!condition)
        {
            throw new Exception($"{name} failed");
        }

        count++;
    }

    private static void Reject(string name, Action operation, ref int count)
    {
        try
        {
            operation();
        }
        catch (ArgumentException)
        {
            count++;
            return;
        }

        throw new Exception($"{name} was accepted");
    }
}
