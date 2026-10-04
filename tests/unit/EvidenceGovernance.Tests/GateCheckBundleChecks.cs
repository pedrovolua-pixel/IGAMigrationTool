using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EvidenceGovernance;

internal static class GateCheckBundleChecks
{
    private const string Commit = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Artifact = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly string[] RequiredChecks = ["BUILD", "SOURCE-SAFETY"];

    public static int Run()
    {
        using var signer = RSA.Create(2048);
        using var otherSigner = RSA.Create(2048);
        using var trustedKey = RSA.Create();
        trustedKey.ImportParameters(signer.ExportParameters(false));

        var count = 0;
        var valid = Bundle();
        Expect("complete trusted bundle", GateCheckBundleResult.Valid, Verify(valid, signer, trustedKey), ref count);
        Expect("missing check", GateCheckBundleResult.ChecksNotPassed,
            Verify(Bundle(checks: [new("BUILD", "PASS")]), signer, trustedKey), ref count);
        Expect("failed check", GateCheckBundleResult.ChecksNotPassed,
            Verify(Bundle(checks: [new("BUILD", "PASS"), new("SOURCE-SAFETY", "FAIL")]), signer, trustedKey), ref count);
        Expect("unexecuted check", GateCheckBundleResult.ChecksNotPassed,
            Verify(Bundle(checks: [new("BUILD", "PASS"), new("SOURCE-SAFETY", "NOT_VERIFIED")]), signer, trustedKey), ref count);
        Expect("unexpected check", GateCheckBundleResult.InvalidSchema,
            Verify(Bundle(checks: [new("BUILD", "PASS"), new("SOURCE-SAFETY", "PASS"), new("EXTRA", "PASS")]), signer, trustedKey), ref count);
        Expect("duplicate check", GateCheckBundleResult.InvalidSchema,
            Verify(Bundle(checks: [new("BUILD", "PASS"), new("BUILD", "PASS")]), signer, trustedKey), ref count);
        Expect("wrong artifact", GateCheckBundleResult.WrongScope,
            Verify(Bundle(artifact: new string('c', 64)), signer, trustedKey), ref count);
        Expect("wrong commit", GateCheckBundleResult.WrongScope,
            Verify(Bundle(commit: new string('c', 40)), signer, trustedKey), ref count);
        Expect("wrong gate", GateCheckBundleResult.WrongScope,
            Verify(Bundle(gate: "G2"), signer, trustedKey), ref count);
        Expect("expired evidence", GateCheckBundleResult.Expired,
            Verify(Bundle(decisionAt: Now.AddMonths(-12)), signer, trustedKey, Now.AddMonths(-12)), ref count);
        Expect("future decision", GateCheckBundleResult.Expired,
            Verify(Bundle(decisionAt: Now.AddSeconds(1)), signer, trustedKey, Now.AddSeconds(1)), ref count);
        Expect("current evidence before expiry", GateCheckBundleResult.Valid,
            Verify(Bundle(decisionAt: Now.AddMonths(-12).AddSeconds(1)), signer, trustedKey,
                Now.AddMonths(-12).AddSeconds(1)), ref count);
        Expect("wrong trusted decision time", GateCheckBundleResult.WrongScope,
            Verify(valid, signer, trustedKey, Now.AddHours(-2)), ref count);

        var withPayload = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(valid)
            .Replace("\"checks\":", "\"customerPayload\":\"secret\",\"checks\":", StringComparison.Ordinal));
        Expect("unknown payload field", GateCheckBundleResult.InvalidSchema,
            Verify(withPayload, signer, trustedKey), ref count);
        var withDuplicateField = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(valid)
            .Replace("\"gate\":\"G1\",", "\"gate\":\"G1\",\"gate\":\"G1\",", StringComparison.Ordinal));
        Expect("duplicate JSON field", GateCheckBundleResult.InvalidSchema,
            Verify(withDuplicateField, signer, trustedKey), ref count);
        var withoutTimeZone = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(valid)
            .Replace("+00:00", "", StringComparison.Ordinal));
        Expect("decision time without UTC zone", GateCheckBundleResult.InvalidSchema,
            Verify(withoutTimeZone, signer, trustedKey), ref count);
        var wrongTimeZone = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(valid)
            .Replace("+00:00", "+01:00", StringComparison.Ordinal));
        Expect("decision time with non-UTC zone", GateCheckBundleResult.InvalidSchema,
            Verify(wrongTimeZone, signer, trustedKey), ref count);

        var signature = signer.SignData(valid, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        var changed = (byte[])valid.Clone();
        changed[^1] ^= 1;
        Expect("tampered bundle", GateCheckBundleResult.Untrusted,
            GateCheckBundleVerifier.Verify(changed, Convert.ToHexString(SHA256.HashData(valid)), signature,
                trustedKey, "G1", Commit, Artifact, RequiredChecks, Now.AddHours(-1), Now), ref count);
        Expect("wrong signer", GateCheckBundleResult.Untrusted,
            Verify(valid, otherSigner, trustedKey), ref count);
        Expect("unsigned bundle", GateCheckBundleResult.Untrusted,
            GateCheckBundleVerifier.Verify(valid, Convert.ToHexString(SHA256.HashData(valid)), [],
                trustedKey, "G1", Commit, Artifact, RequiredChecks, Now.AddHours(-1), Now), ref count);
        Expect("invalid required-check configuration", GateCheckBundleResult.InvalidInput,
            GateCheckBundleVerifier.Verify(valid, Convert.ToHexString(SHA256.HashData(valid)), signature,
                trustedKey, "G1", Commit, Artifact, ["BUILD", "BUILD"], Now.AddHours(-1), Now), ref count);
        Expect("missing required-check configuration", GateCheckBundleResult.InvalidInput,
            GateCheckBundleVerifier.Verify(valid, Convert.ToHexString(SHA256.HashData(valid)), signature,
                trustedKey, "G1", Commit, Artifact, null, Now.AddHours(-1), Now), ref count);
        Expect("missing expected artifact identity", GateCheckBundleResult.InvalidInput,
            GateCheckBundleVerifier.Verify(valid, Convert.ToHexString(SHA256.HashData(valid)), signature,
                trustedKey, "G1", Commit, null, RequiredChecks, Now.AddHours(-1), Now), ref count);
        Expect("far-future decision", GateCheckBundleResult.Expired,
            Verify(Bundle(decisionAt: DateTimeOffset.MaxValue), signer, trustedKey, DateTimeOffset.MaxValue), ref count);

        return count;
    }

    private static byte[] Bundle(
        string gate = "G1",
        string commit = Commit,
        string artifact = Artifact,
        DateTimeOffset? decisionAt = null,
        Check[]? checks = null)
    {
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1,
            gate,
            commitSha = commit,
            artifactSha256 = artifact,
            decisionAt = decisionAt ?? Now.AddHours(-1),
            checks = checks ?? [new("BUILD", "PASS"), new("SOURCE-SAFETY", "PASS")]
        });
    }

    private static GateCheckBundleResult Verify(byte[] bundle, RSA signer, RSA trustedKey,
        DateTimeOffset? expectedDecisionAt = null)
    {
        return GateCheckBundleVerifier.Verify(bundle, Convert.ToHexString(SHA256.HashData(bundle)),
            signer.SignData(bundle, HashAlgorithmName.SHA256, RSASignaturePadding.Pss),
            trustedKey, "G1", Commit, Artifact, RequiredChecks, expectedDecisionAt ?? Now.AddHours(-1), Now);
    }

    private static void Expect(string caseName, GateCheckBundleResult expected,
        GateCheckBundleResult actual, ref int count)
    {
        if (actual != expected)
        {
            throw new Exception($"{caseName}: expected {expected}, received {actual}");
        }

        count++;
    }

    private sealed record Check(string id, string result);
}
