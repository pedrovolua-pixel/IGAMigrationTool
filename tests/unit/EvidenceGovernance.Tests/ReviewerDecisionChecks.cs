using System.Security.Cryptography;
using System.Text;
using EvidenceGovernance;

internal static class ReviewerDecisionChecks
{
    private const string Artifact = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string Scope = "PILOT-A";
    private const string Reviewer = "REVIEWER-1";
    private const string Role = "SECURITY";
    private static readonly DateTimeOffset DecisionAt = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = DecisionAt.AddHours(1);

    public static int Run()
    {
        using var signer = RSA.Create(2048);
        using var otherSigner = RSA.Create(2048);
        using var trustedKey = RSA.Create();
        trustedKey.ImportParameters(signer.ExportParameters(false));
        var approval = ReviewerDecisionBundle.Create(Artifact, Scope, Reviewer, Role,
            DecisionAt, ReviewerDecision.Approve);
        var signature = Sign(signer, approval);

        var count = 0;
        Expect("signed approval", ReviewerDecisionResult.ValidApproval,
            Verify(approval, signer, trustedKey), ref count);
        var rejected = ReviewerDecisionBundle.Create(Artifact, Scope, Reviewer, Role,
            DecisionAt, ReviewerDecision.Reject);
        Expect("signed rejection remains rejection", ReviewerDecisionResult.Rejected,
            Verify(rejected, signer, trustedKey), ref count);
        Expect("wrong artifact", ReviewerDecisionResult.WrongScope,
            Verify(approval, signer, trustedKey, artifact: new string('c', 64)), ref count);
        Expect("cross-scope replay", ReviewerDecisionResult.WrongScope,
            Verify(approval, signer, trustedKey, scope: "PILOT-B"), ref count);
        Expect("wrong reviewer identity", ReviewerDecisionResult.WrongScope,
            Verify(approval, signer, trustedKey, reviewer: "REVIEWER-2"), ref count);
        Expect("wrong reviewer role", ReviewerDecisionResult.WrongScope,
            Verify(approval, signer, trustedKey, role: "QUALITY"), ref count);
        Expect("wrong trusted decision time", ReviewerDecisionResult.WrongScope,
            Verify(approval, signer, trustedKey, expectedDecision: DecisionAt.AddMinutes(-1)), ref count);
        Expect("wrong trusted key", ReviewerDecisionResult.Untrusted,
            Verify(approval, signer, otherSigner), ref count);
        Expect("unsigned approval", ReviewerDecisionResult.Untrusted,
            ReviewerDecisionBundle.Verify(approval, Digest(approval), [], trustedKey,
                Artifact, Scope, Reviewer, Role, DecisionAt, Now), ref count);
        var changed = (byte[])approval.Clone();
        changed[^1] ^= 1;
        Expect("tampered approval with forged digest", ReviewerDecisionResult.Untrusted,
            ReviewerDecisionBundle.Verify(changed, Digest(changed), signature, trustedKey,
                Artifact, Scope, Reviewer, Role, DecisionAt, Now), ref count);
        Expect("expired approval", ReviewerDecisionResult.Expired,
            Verify(approval, signer, trustedKey, now: DecisionAt.AddMonths(12)), ref count);
        Expect("future approval", ReviewerDecisionResult.Expired,
            Verify(approval, signer, trustedKey, now: DecisionAt.AddTicks(-1)), ref count);

        var withPayload = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(approval)
            .Replace("\"decision\":", "\"customerPayload\":\"secret\",\"decision\":", StringComparison.Ordinal));
        Expect("payload field rejected", ReviewerDecisionResult.InvalidSchema,
            Verify(withPayload, signer, trustedKey), ref count);
        var withDuplicate = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(approval)
            .Replace("\"scopeId\":\"PILOT-A\",", "\"scopeId\":\"PILOT-A\",\"scopeId\":\"PILOT-A\",",
                StringComparison.Ordinal));
        Expect("duplicate JSON field rejected", ReviewerDecisionResult.InvalidSchema,
            Verify(withDuplicate, signer, trustedKey), ref count);
        var withoutZone = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(approval)
            .Replace("+00:00", "", StringComparison.Ordinal));
        Expect("decision without UTC zone rejected", ReviewerDecisionResult.InvalidSchema,
            Verify(withoutZone, signer, trustedKey), ref count);

        Reject("free-text reviewer", () => ReviewerDecisionBundle.Create(Artifact, Scope,
            "Alice Example", Role, DecisionAt, ReviewerDecision.Approve), ref count);
        Reject("invalid digest", () => ReviewerDecisionBundle.Create("bad", Scope,
            Reviewer, Role, DecisionAt, ReviewerDecision.Approve), ref count);
        Reject("non-UTC decision", () => ReviewerDecisionBundle.Create(Artifact, Scope,
            Reviewer, Role, DecisionAt.ToOffset(TimeSpan.FromHours(-4)), ReviewerDecision.Approve), ref count);
        Reject("invalid decision enum", () => ReviewerDecisionBundle.Create(Artifact, Scope,
            Reviewer, Role, DecisionAt, (ReviewerDecision)99), ref count);
        return count;
    }

    private static ReviewerDecisionResult Verify(byte[] bundle, RSA signer, RSA trustedKey,
        string artifact = Artifact, string scope = Scope, string reviewer = Reviewer,
        string role = Role, DateTimeOffset? expectedDecision = null,
        DateTimeOffset? now = null) => ReviewerDecisionBundle.Verify(
        bundle, Digest(bundle), Sign(signer, bundle), trustedKey,
        artifact, scope, reviewer, role, expectedDecision ?? DecisionAt, now ?? Now);

    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static byte[] Sign(RSA signer, byte[] bytes) =>
        signer.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);

    private static void Expect(string name, ReviewerDecisionResult expected,
        ReviewerDecisionResult actual, ref int count)
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

        throw new Exception($"{name}: invalid review metadata was accepted.");
    }
}
