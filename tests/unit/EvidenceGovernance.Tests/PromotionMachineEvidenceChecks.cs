using System.Security.Cryptography;
using System.Text;
using EvidenceGovernance;

internal static class PromotionMachineEvidenceChecks
{
    private const string Commit = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly DateTimeOffset DecisionAt = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = DecisionAt.AddHours(1);

    public static int Run()
    {
        using var artifactSigner = RSA.Create(2048);
        using var gateSigner = RSA.Create(2048);
        using var otherSigner = RSA.Create(2048);
        using var artifactPublicKey = RSA.Create();
        artifactPublicKey.ImportParameters(artifactSigner.ExportParameters(false));

        var artifact = Encoding.UTF8.GetBytes("synthetic signed build artifact");
        var artifactDigest = Convert.ToHexString(SHA256.HashData(artifact));
        var artifactSignature = artifactSigner.SignData(artifact, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        var trust = Registry(gateSigner, DecisionAt.AddDays(-1), DecisionAt.AddDays(1));
        var bundle = Bundle(artifactDigest, CheckOutcome.Pass, DecisionAt);
        var bundleDigest = Convert.ToHexString(SHA256.HashData(bundle));
        var bundleSignature = gateSigner.SignData(bundle, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);

        PromotionMachineEvidenceResult Evaluate(
            byte[]? artifactBytes = null, string? expectedDigest = null, byte[]? artifactSig = null,
            GateSigningTrustRegistry? trustedKeys = null, string? keyId = "KEY-1",
            byte[]? bundleBytes = null, string? bundleHash = null, byte[]? bundleSig = null,
            string expectedCommit = Commit, DateTimeOffset? expectedDecision = null,
            DateTimeOffset? evaluationTime = null) => PromotionMachineEvidencePreflight.Evaluate(
                artifactBytes ?? artifact, expectedDigest ?? artifactDigest,
                artifactSig ?? artifactSignature, artifactPublicKey,
                trustedKeys ?? trust, keyId,
                bundleBytes ?? bundle, bundleHash ?? bundleDigest, bundleSig ?? bundleSignature,
                "G1", expectedCommit, ["BUILD"], expectedDecision ?? DecisionAt,
                evaluationTime ?? Now);

        var count = 0;
        Expect("signed matching artifact and gate", true, Evaluate().ReadyForReview, ref count);
        Expect("tampered artifact", ArtifactIntegrityResult.DigestMismatch,
            Evaluate(artifactBytes: Encoding.UTF8.GetBytes("changed artifact")).Artifact, ref count);
        Expect("forged artifact digest", ArtifactIntegrityResult.SignatureMismatch,
            Evaluate(artifactBytes: Encoding.UTF8.GetBytes("changed artifact"),
                expectedDigest: Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("changed artifact")))).Artifact,
            ref count);
        Expect("unsigned artifact", ArtifactIntegrityResult.InvalidInput,
            Evaluate(artifactSig: []).Artifact, ref count);
        Expect("unsigned gate bundle", GateCheckBundleResult.Untrusted,
            Evaluate(bundleSig: []).Gate, ref count);
        Expect("missing gate bundle", GateCheckBundleResult.InvalidInput,
            Evaluate(bundleBytes: []).Gate, ref count);

        var tamperedBundle = (byte[])bundle.Clone();
        tamperedBundle[^1] ^= 1;
        Expect("tampered gate bundle", GateCheckBundleResult.Untrusted,
            Evaluate(bundleBytes: tamperedBundle,
                bundleHash: Convert.ToHexString(SHA256.HashData(tamperedBundle))).Gate, ref count);

        var wrongArtifactBundle = Bundle(new string('c', 64), CheckOutcome.Pass, DecisionAt);
        Expect("gate for different artifact", GateCheckBundleResult.WrongScope,
            Evaluate(bundleBytes: wrongArtifactBundle,
                bundleHash: Convert.ToHexString(SHA256.HashData(wrongArtifactBundle)),
                bundleSig: Sign(gateSigner, wrongArtifactBundle)).Gate, ref count);

        var failedBundle = Bundle(artifactDigest, CheckOutcome.Fail, DecisionAt);
        Expect("failed required check", GateCheckBundleResult.ChecksNotPassed,
            Evaluate(bundleBytes: failedBundle,
                bundleHash: Convert.ToHexString(SHA256.HashData(failedBundle)),
                bundleSig: Sign(gateSigner, failedBundle)).Gate, ref count);
        var skippedBundle = Bundle(artifactDigest, CheckOutcome.NotVerified, DecisionAt);
        Expect("unverified required check", GateCheckBundleResult.ChecksNotPassed,
            Evaluate(bundleBytes: skippedBundle,
                bundleHash: Convert.ToHexString(SHA256.HashData(skippedBundle)),
                bundleSig: Sign(gateSigner, skippedBundle)).Gate, ref count);
        Expect("wrong commit", GateCheckBundleResult.WrongScope,
            Evaluate(expectedCommit: new string('d', 40)).Gate, ref count);
        Expect("wrong decision time", GateCheckBundleResult.WrongScope,
            Evaluate(expectedDecision: DecisionAt.AddMinutes(-1)).Gate, ref count);
        Expect("wrong gate signer", GateCheckBundleResult.Untrusted,
            Evaluate(trustedKeys: Registry(otherSigner, DecisionAt.AddDays(-1), DecisionAt.AddDays(1))).Gate,
            ref count);
        Expect("unknown gate key", GateCheckBundleResult.Untrusted,
            Evaluate(keyId: "KEY-2").Gate, ref count);

        var expiredAt = Now.AddMonths(-12);
        var expiredBundle = Bundle(artifactDigest, CheckOutcome.Pass, expiredAt);
        Expect("expired gate evidence", GateCheckBundleResult.Expired,
            Evaluate(trustedKeys: Registry(gateSigner, expiredAt.AddDays(-1), expiredAt.AddDays(1)),
                bundleBytes: expiredBundle,
                bundleHash: Convert.ToHexString(SHA256.HashData(expiredBundle)),
                bundleSig: Sign(gateSigner, expiredBundle), expectedDecision: expiredAt).Gate,
            ref count);
        Expect("missing trust registry", GateCheckBundleResult.InvalidInput,
            PromotionMachineEvidencePreflight.Evaluate(artifact, artifactDigest, artifactSignature,
                artifactPublicKey, null, "KEY-1", bundle, bundleDigest, bundleSignature,
                "G1", Commit, ["BUILD"], DecisionAt, Now).Gate, ref count);
        return count;
    }

    private static GateSigningTrustRegistry Registry(RSA signer, DateTimeOffset validFrom,
        DateTimeOffset validUntilExclusive) => new([
        new TrustedGateSigningKey("KEY-1", signer.ExportSubjectPublicKeyInfo(),
            ["G1"], validFrom, validUntilExclusive, null)
    ]);

    private static byte[] Bundle(string artifactDigest, CheckOutcome outcome,
        DateTimeOffset decisionAt) => GateCheckBundleWriter.Create("G1", Commit,
        artifactDigest, decisionAt, ["BUILD"],
        new Dictionary<string, CheckOutcome> { ["BUILD"] = outcome });

    private static byte[] Sign(RSA signer, byte[] data) =>
        signer.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);

    private static void Expect<T>(string name, T expected, T actual, ref int count)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"{name}: expected {expected}, received {actual}.");
        }

        count++;
    }
}
