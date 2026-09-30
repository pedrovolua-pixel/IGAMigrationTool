using System.Security.Cryptography;
using EvidenceGovernance;

internal static class ReviewerTrustChecks
{
    private const string Artifact = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string Scope = "PILOT-A";
    private static readonly DateTimeOffset DecisionAt = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = DecisionAt.AddHours(1);

    public static int Run()
    {
        using var securitySigner = RSA.Create(2048);
        using var qualitySigner = RSA.Create(2048);
        using var otherSigner = RSA.Create(2048);
        var securityPublicKey = securitySigner.ExportSubjectPublicKeyInfo();
        var securityTrust = Entry("SEC-KEY", securityPublicKey, "REVIEWER-1", "SECURITY",
            [Scope], DecisionAt.AddDays(-1), DecisionAt.AddDays(1));
        var qualityTrust = Entry("QA-KEY", qualitySigner.ExportSubjectPublicKeyInfo(), "REVIEWER-2", "QUALITY",
            [Scope], DecisionAt.AddDays(-1), DecisionAt.AddDays(1));
        var registry = new ReviewerTrustRegistry([securityTrust, qualityTrust]);
        var securityBundle = Bundle("REVIEWER-1", "SECURITY", ReviewerDecision.Approve);
        var qualityBundle = Bundle("REVIEWER-2", "QUALITY", ReviewerDecision.Approve);

        var count = 0;
        var security = Verify(registry, "SEC-KEY", securityBundle, securitySigner);
        var quality = Verify(registry, "QA-KEY", qualityBundle, qualitySigner);
        Expect("trusted security approval", ReviewerDecisionResult.ValidApproval, security.Result, ref count);
        Expect("trusted quality approval", ReviewerDecisionResult.ValidApproval, quality.Result, ref count);
        Expect("verified reviewer identity", "REVIEWER-1", security.Approval?.ReviewerId, ref count);
        Expect("verified reviewer role", "SECURITY", security.Approval?.Role, ref count);
        Expect("unknown key", ReviewerDecisionResult.Untrusted,
            Verify(registry, "OTHER-KEY", securityBundle, securitySigner).Result, ref count);
        Expect("wrong signing key", ReviewerDecisionResult.Untrusted,
            Verify(registry, "SEC-KEY", securityBundle, otherSigner).Result, ref count);
        Expect("claimed wrong role", ReviewerDecisionResult.WrongScope,
            Verify(new ReviewerTrustRegistry([securityTrust with { Role = "QUALITY" }]),
                "SEC-KEY", securityBundle, securitySigner).Result, ref count);
        Expect("cross-scope key", ReviewerDecisionResult.Untrusted,
            Verify(new ReviewerTrustRegistry([securityTrust with { AllowedScopes = ["PILOT-B"] }]),
                "SEC-KEY", securityBundle, securitySigner).Result, ref count);
        Expect("revoked reviewer key", ReviewerDecisionResult.Untrusted,
            Verify(new ReviewerTrustRegistry([securityTrust with { RevokedAt = DecisionAt.AddMinutes(1) }]),
                "SEC-KEY", securityBundle, securitySigner).Result, ref count);
        Expect("historical key after rotation", ReviewerDecisionResult.ValidApproval,
            Verify(new ReviewerTrustRegistry([securityTrust with { ValidUntilExclusive = DecisionAt.AddMinutes(1) }]),
                "SEC-KEY", securityBundle, securitySigner).Result, ref count);
        Expect("decision outside key validity", ReviewerDecisionResult.Untrusted,
            Verify(new ReviewerTrustRegistry([securityTrust with { ValidFrom = DecisionAt.AddMinutes(1) }]),
                "SEC-KEY", securityBundle, securitySigner).Result, ref count);
        var rejection = Bundle("REVIEWER-1", "SECURITY", ReviewerDecision.Reject);
        var rejected = Verify(registry, "SEC-KEY", rejection, securitySigner);
        Expect("signed rejection", ReviewerDecisionResult.Rejected, rejected.Result, ref count);
        Expect("rejection grants no approval", true, rejected.Approval is null, ref count);

        Expect("complete independent review", ReviewEvidenceCoverageResult.CompleteForReview,
            ReviewEvidenceCoverage.Evaluate(Artifact, Scope, "REVIEWER-1", ["SECURITY", "QUALITY"],
                [security.Approval!, quality.Approval!], Now), ref count);
        Expect("missing required role", ReviewEvidenceCoverageResult.MissingRequiredRole,
            ReviewEvidenceCoverage.Evaluate(Artifact, Scope, "REVIEWER-1", ["SECURITY", "QUALITY"],
                [security.Approval!], Now), ref count);
        Expect("author only", ReviewEvidenceCoverageResult.AuthorOnly,
            ReviewEvidenceCoverage.Evaluate(Artifact, Scope, "REVIEWER-1", ["SECURITY"],
                [security.Approval!], Now), ref count);
        Expect("duplicate approval", ReviewEvidenceCoverageResult.DuplicateApproval,
            ReviewEvidenceCoverage.Evaluate(Artifact, Scope, "REVIEWER-2", ["SECURITY"],
                [security.Approval!, security.Approval!], Now), ref count);
        Expect("wrong artifact proof", ReviewEvidenceCoverageResult.WrongScope,
            ReviewEvidenceCoverage.Evaluate(new string('c', 64), Scope, "REVIEWER-2", ["SECURITY"],
                [security.Approval!], Now), ref count);
        Expect("replayed proof after trust check", ReviewEvidenceCoverageResult.StaleVerification,
            ReviewEvidenceCoverage.Evaluate(Artifact, Scope, "REVIEWER-2", ["SECURITY"],
                [security.Approval!], Now.AddTicks(1)), ref count);

        Reject("duplicate reviewer key", () => new ReviewerTrustRegistry([securityTrust, securityTrust]), ref count);
        Reject("same public key under another role", () => new ReviewerTrustRegistry([
            securityTrust, securityTrust with { KeyId = "SEC-KEY-2", Role = "QUALITY" }
        ]), ref count);
        Reject("invalid reviewer public key", () => new ReviewerTrustRegistry([
            securityTrust with { SubjectPublicKeyInfo = [1, 2, 3] }
        ]), ref count);

        Array.Fill(securityPublicKey, (byte)0);
        Expect("public key snapshot", ReviewerDecisionResult.ValidApproval,
            Verify(registry, "SEC-KEY", securityBundle, securitySigner).Result, ref count);
        return count;
    }

    private static TrustedReviewerSigningKey Entry(string keyId, byte[] publicKey,
        string reviewerId, string role, IReadOnlyCollection<string> scopes,
        DateTimeOffset validFrom, DateTimeOffset validUntilExclusive) =>
        new(keyId, publicKey, reviewerId, role, scopes, validFrom, validUntilExclusive, null);

    private static byte[] Bundle(string reviewerId, string role, ReviewerDecision decision) =>
        ReviewerDecisionBundle.Create(Artifact, Scope, reviewerId, role, DecisionAt, decision);

    private static ReviewerVerification Verify(ReviewerTrustRegistry registry, string keyId,
        byte[] bundle, RSA signer) => registry.VerifyApproval(keyId, bundle,
        Convert.ToHexString(SHA256.HashData(bundle)),
        signer.SignData(bundle, HashAlgorithmName.SHA256, RSASignaturePadding.Pss),
        Artifact, Scope, DecisionAt, Now);

    private static void Expect<T>(string name, T expected, T actual, ref int count)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
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

        throw new Exception($"{name}: invalid reviewer trust was accepted.");
    }
}
