using System.Security.Cryptography;

namespace EvidenceGovernance;

/// <summary>
/// Verifies normal-path machine and reviewer evidence together. Compatibility,
/// owner overrides, audit and human activation are separate required steps.
/// </summary>
public static class NormalPromotionEvidencePreflight
{
    public static NormalPromotionEvidenceResult Evaluate(
        ReadOnlySpan<byte> artifact,
        string? expectedArtifactSha256,
        ReadOnlySpan<byte> artifactSignature,
        RSA? trustedArtifactKey,
        GateSigningTrustRegistry? gateTrust,
        string? gateKeyId,
        ReadOnlySpan<byte> gateBundle,
        string? gateBundleSha256,
        ReadOnlySpan<byte> gateBundleSignature,
        string? expectedGate,
        string? expectedCommitSha,
        IReadOnlyCollection<string>? requiredChecks,
        DateTimeOffset expectedGateDecisionAt,
        ReviewerTrustRegistry? reviewerTrust,
        string? expectedScopeId,
        string? authorId,
        IReadOnlyCollection<string>? requiredReviewerRoles,
        IReadOnlyCollection<SignedReviewerEvidence>? reviewerDecisions,
        DateTimeOffset now)
    {
        var machine = PromotionMachineEvidencePreflight.Evaluate(artifact, expectedArtifactSha256,
            artifactSignature, trustedArtifactKey, gateTrust, gateKeyId, gateBundle,
            gateBundleSha256, gateBundleSignature, expectedGate, expectedCommitSha,
            requiredChecks, expectedGateDecisionAt, now);
        if (!machine.ReadyForReview)
        {
            return new NormalPromotionEvidenceResult(machine, null, null);
        }

        if (reviewerTrust is null || reviewerDecisions is null ||
            !GateCheckBundleRules.IsCheckId(expectedScopeId) ||
            !GateCheckBundleRules.IsCheckId(authorId))
        {
            return new NormalPromotionEvidenceResult(machine, ReviewerDecisionResult.InvalidInput, null);
        }

        var approvals = new List<VerifiedReviewerApproval>(reviewerDecisions.Count);
        foreach (var item in reviewerDecisions)
        {
            if (item is null || item.Bundle is null || item.Signature is null)
            {
                return new NormalPromotionEvidenceResult(machine, ReviewerDecisionResult.InvalidInput, null);
            }

            var verified = reviewerTrust.VerifyApproval(item.KeyId, item.Bundle,
                item.BundleSha256, item.Signature, expectedArtifactSha256,
                expectedScopeId, item.DecisionAt, now);
            if (verified.Result != ReviewerDecisionResult.ValidApproval || verified.Approval is null)
            {
                return new NormalPromotionEvidenceResult(machine, verified.Result, null);
            }

            approvals.Add(verified.Approval);
        }

        return new NormalPromotionEvidenceResult(machine, null,
            ReviewEvidenceCoverage.Evaluate(expectedArtifactSha256, expectedScopeId,
                authorId, requiredReviewerRoles, approvals, now));
    }
}

public sealed record SignedReviewerEvidence(string KeyId, byte[] Bundle,
    string BundleSha256, byte[] Signature, DateTimeOffset DecisionAt);

public readonly record struct NormalPromotionEvidenceResult(
    PromotionMachineEvidenceResult Machine,
    ReviewerDecisionResult? ReviewerDecision,
    ReviewEvidenceCoverageResult? ReviewCoverage)
{
    public bool ReadyForCompatibilityReview => Machine.ReadyForReview &&
        ReviewerDecision is null && ReviewCoverage == ReviewEvidenceCoverageResult.CompleteForReview;
}
