using System.Security.Cryptography;
using System.Text;
using EvidenceGovernance;

internal static class NormalPromotionEvidenceChecks
{
    private const string Commit = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Scope = "PILOT-A";
    private static readonly DateTimeOffset DecisionAt = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = DecisionAt.AddHours(1);

    public static int Run()
    {
        using var artifactSigner = RSA.Create(2048);
        using var gateSigner = RSA.Create(2048);
        using var securitySigner = RSA.Create(2048);
        using var qualitySigner = RSA.Create(2048);
        using var artifactPublicKey = RSA.Create();
        artifactPublicKey.ImportParameters(artifactSigner.ExportParameters(false));
        var artifact = Encoding.UTF8.GetBytes("synthetic promotion candidate");
        var digest = Digest(artifact);
        var artifactSignature = Sign(artifactSigner, artifact);
        var gateBundle = GateCheckBundleWriter.Create("G1", Commit, digest, DecisionAt,
            ["BUILD"], new Dictionary<string, CheckOutcome> { ["BUILD"] = CheckOutcome.Pass });
        var gateSignature = Sign(gateSigner, gateBundle);
        var gateTrust = new GateSigningTrustRegistry([
            new TrustedGateSigningKey("GATE-KEY", gateSigner.ExportSubjectPublicKeyInfo(),
                ["G1"], DecisionAt.AddDays(-1), DecisionAt.AddDays(1), null)
        ]);
        var securityEntry = new TrustedReviewerSigningKey("SEC-KEY", securitySigner.ExportSubjectPublicKeyInfo(),
            "AUTHOR-1", "SECURITY", [Scope], DecisionAt.AddDays(-1), DecisionAt.AddDays(1), null);
        var qualityEntry = new TrustedReviewerSigningKey("QA-KEY", qualitySigner.ExportSubjectPublicKeyInfo(),
            "REVIEWER-2", "QUALITY", [Scope], DecisionAt.AddDays(-1), DecisionAt.AddDays(1), null);
        var reviewerTrust = new ReviewerTrustRegistry([securityEntry, qualityEntry]);

        SignedReviewerEvidence Decision(string keyId, RSA signer, string reviewerId,
            string role, string scope = Scope, string? artifactDigest = null,
            ReviewerDecision outcome = ReviewerDecision.Approve)
        {
            var bytes = ReviewerDecisionBundle.Create(artifactDigest ?? digest, scope,
                reviewerId, role, DecisionAt, outcome);
            return new SignedReviewerEvidence(keyId, bytes, Digest(bytes), Sign(signer, bytes), DecisionAt);
        }

        var security = Decision("SEC-KEY", securitySigner, "AUTHOR-1", "SECURITY");
        var quality = Decision("QA-KEY", qualitySigner, "REVIEWER-2", "QUALITY");

        NormalPromotionEvidenceResult Evaluate(
            IReadOnlyCollection<SignedReviewerEvidence>? decisions = null,
            IReadOnlyCollection<string>? roles = null,
            ReviewerTrustRegistry? trustedReviewers = null,
            byte[]? artifactBytes = null,
            string scope = Scope) => NormalPromotionEvidencePreflight.Evaluate(
                artifactBytes ?? artifact, digest, artifactSignature, artifactPublicKey,
                gateTrust, "GATE-KEY", gateBundle, Digest(gateBundle), gateSignature,
                "G1", Commit, ["BUILD"], DecisionAt,
                trustedReviewers ?? reviewerTrust, scope, "AUTHOR-1",
                roles ?? ["SECURITY", "QUALITY"], decisions ?? [security, quality], Now);

        var count = 0;
        Expect("complete normal evidence", true, Evaluate().ReadyForCompatibilityReview, ref count);
        Expect("missing reviewers", ReviewEvidenceCoverageResult.MissingRequiredRole,
            Evaluate(decisions: []).ReviewCoverage, ref count);
        Expect("author-only review", ReviewEvidenceCoverageResult.AuthorOnly,
            Evaluate(decisions: [security], roles: ["SECURITY"]).ReviewCoverage, ref count);
        var wrongRole = Decision("SEC-KEY", securitySigner, "AUTHOR-1", "QUALITY");
        Expect("claimed wrong reviewer role", ReviewerDecisionResult.WrongScope,
            Evaluate(decisions: [wrongRole, quality]).ReviewerDecision, ref count);
        var wrongScope = Decision("SEC-KEY", securitySigner, "AUTHOR-1", "SECURITY", "PILOT-B");
        Expect("cross-scope decision", ReviewerDecisionResult.WrongScope,
            Evaluate(decisions: [wrongScope, quality]).ReviewerDecision, ref count);
        var wrongArtifact = Decision("SEC-KEY", securitySigner, "AUTHOR-1", "SECURITY",
            artifactDigest: new string('c', 64));
        Expect("decision for different artifact", ReviewerDecisionResult.WrongScope,
            Evaluate(decisions: [wrongArtifact, quality]).ReviewerDecision, ref count);
        var rejection = Decision("SEC-KEY", securitySigner, "AUTHOR-1", "SECURITY",
            outcome: ReviewerDecision.Reject);
        Expect("signed rejection blocks", ReviewerDecisionResult.Rejected,
            Evaluate(decisions: [rejection, quality]).ReviewerDecision, ref count);
        var tampered = (byte[])security.Bundle.Clone();
        tampered[^1] ^= 1;
        Expect("tampered reviewer decision", ReviewerDecisionResult.Untrusted,
            Evaluate(decisions: [security with { Bundle = tampered, BundleSha256 = Digest(tampered) }, quality])
                .ReviewerDecision, ref count);
        Expect("revoked reviewer key", ReviewerDecisionResult.Untrusted,
            Evaluate(trustedReviewers: new ReviewerTrustRegistry([
                securityEntry with { RevokedAt = DecisionAt.AddMinutes(1) }, qualityEntry
            ])).ReviewerDecision, ref count);
        Expect("tampered artifact short-circuits review", ArtifactIntegrityResult.DigestMismatch,
            Evaluate(artifactBytes: Encoding.UTF8.GetBytes("changed artifact")).Machine.Artifact, ref count);
        Expect("no reviewer result after machine failure", true,
            Evaluate(artifactBytes: Encoding.UTF8.GetBytes("changed artifact")).ReviewCoverage is null,
            ref count);
        Expect("wrong requested scope", ReviewerDecisionResult.Untrusted,
            Evaluate(scope: "PILOT-B").ReviewerDecision, ref count);
        return count;
    }

    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static byte[] Sign(RSA signer, byte[] bytes) =>
        signer.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);

    private static void Expect<T>(string name, T expected, T actual, ref int count)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"{name}: expected {expected}, received {actual}.");
        }

        count++;
    }
}
