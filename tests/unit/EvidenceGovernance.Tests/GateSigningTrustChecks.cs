using System.Security.Cryptography;
using EvidenceGovernance;

internal static class GateSigningTrustChecks
{
    private const string Commit = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Artifact = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private static readonly DateTimeOffset DecisionAt = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = DecisionAt.AddDays(1);

    public static int Run()
    {
        using var signer = RSA.Create(2048);
        using var otherSigner = RSA.Create(2048);
        var publicKey = signer.ExportSubjectPublicKeyInfo();
        var trust = Entry("KEY-1", publicKey, ["G1"], DecisionAt.AddDays(-1), DecisionAt.AddDays(1));
        var registry = new GateSigningTrustRegistry([trust]);
        var bundle = GateCheckBundleWriter.Create("G1", Commit, Artifact, DecisionAt,
            ["BUILD"], new Dictionary<string, CheckOutcome> { ["BUILD"] = CheckOutcome.Pass });
        var signature = signer.SignData(bundle, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        var digest = Convert.ToHexString(SHA256.HashData(bundle));

        var count = 0;
        Expect("trusted historical signature after rotation", GateCheckBundleResult.Valid,
            Verify(registry, "KEY-1", bundle, digest, signature), ref count);
        Expect("unknown key", GateCheckBundleResult.Untrusted,
            Verify(registry, "KEY-2", bundle, digest, signature), ref count);
        Expect("malformed key identity", GateCheckBundleResult.InvalidInput,
            Verify(registry, "free text", bundle, digest, signature), ref count);
        Expect("wrong signature", GateCheckBundleResult.Untrusted,
            Verify(registry, "KEY-1", bundle, digest,
                otherSigner.SignData(bundle, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)), ref count);
        Expect("wrong gate scope", GateCheckBundleResult.Untrusted,
            Verify(new GateSigningTrustRegistry([Entry("KEY-1", publicKey, ["G2"],
                DecisionAt.AddDays(-1), DecisionAt.AddDays(1))]), "KEY-1", bundle, digest, signature), ref count);
        Expect("signing outside key validity", GateCheckBundleResult.Untrusted,
            Verify(new GateSigningTrustRegistry([Entry("KEY-1", publicKey, ["G1"],
                DecisionAt.AddDays(1), DecisionAt.AddDays(2))]), "KEY-1", bundle, digest, signature), ref count);
        Expect("revoked key", GateCheckBundleResult.Untrusted,
            Verify(new GateSigningTrustRegistry([trust with { RevokedAt = DecisionAt.AddHours(1) }]),
                "KEY-1", bundle, digest, signature), ref count);
        Expect("revocation not yet effective", GateCheckBundleResult.Valid,
            Verify(new GateSigningTrustRegistry([trust with { RevokedAt = Now.AddHours(1) }]),
                "KEY-1", bundle, digest, signature), ref count);

        Reject("duplicate key identity", () => new GateSigningTrustRegistry([trust, trust]), ref count);
        Reject("duplicate key material", () => new GateSigningTrustRegistry([
            Entry("KEY-1", signer.ExportSubjectPublicKeyInfo(), ["G1"], DecisionAt, Now),
            Entry("KEY-2", signer.ExportSubjectPublicKeyInfo(), ["G2"], DecisionAt, Now)
        ]), ref count);
        Reject("invalid public key", () => new GateSigningTrustRegistry([
            Entry("KEY-3", [1, 2, 3], ["G1"], DecisionAt, Now)
        ]), ref count);
        Reject("duplicate gate scope", () => new GateSigningTrustRegistry([
            Entry("KEY-4", signer.ExportSubjectPublicKeyInfo(), ["G1", "G1"], DecisionAt, Now)
        ]), ref count);

        Array.Fill(publicKey, (byte)0);
        Expect("trusted key is snapshotted", GateCheckBundleResult.Valid,
            Verify(registry, "KEY-1", bundle, digest, signature), ref count);
        return count;
    }

    private static TrustedGateSigningKey Entry(string keyId, byte[] publicKey,
        IReadOnlyCollection<string> gates, DateTimeOffset validFrom, DateTimeOffset validUntilExclusive) =>
        new(keyId, publicKey, gates, validFrom, validUntilExclusive, null);

    private static GateCheckBundleResult Verify(GateSigningTrustRegistry registry, string keyId,
        byte[] bundle, string digest, byte[] signature) => registry.VerifyBundle(keyId, bundle,
        digest, signature, "G1", Commit, Artifact, ["BUILD"], DecisionAt, Now);

    private static void Expect(string name, GateCheckBundleResult expected,
        GateCheckBundleResult actual, ref int count)
    {
        if (actual != expected)
        {
            throw new Exception($"{name}: expected {expected}, received {actual}.");
        }

        count++;
    }

    private static void Reject(string name, Action action, ref int count)
    {
        try
        {
            action();
        }
        catch (ArgumentException)
        {
            count++;
            return;
        }

        throw new Exception($"{name}: invalid trust entry was accepted.");
    }
}
