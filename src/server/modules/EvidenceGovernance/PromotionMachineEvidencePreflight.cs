using System.Security.Cryptography;

namespace EvidenceGovernance;

/// <summary>
/// Checks the two signed machine artifacts required before a normal promotion
/// review can begin. It does not authenticate reviewers or activate anything.
/// </summary>
public static class PromotionMachineEvidencePreflight
{
    public static PromotionMachineEvidenceResult Evaluate(
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
        DateTimeOffset expectedDecisionAt,
        DateTimeOffset now)
    {
        var artifactResult = ArtifactIntegrityVerifier.Verify(
            artifact, expectedArtifactSha256, artifactSignature, trustedArtifactKey);
        if (artifactResult != ArtifactIntegrityResult.Valid)
        {
            return new PromotionMachineEvidenceResult(artifactResult, null);
        }

        if (gateTrust is null)
        {
            return new PromotionMachineEvidenceResult(artifactResult, GateCheckBundleResult.InvalidInput);
        }

        var gateResult = gateTrust.VerifyBundle(gateKeyId, gateBundle, gateBundleSha256,
            gateBundleSignature, expectedGate, expectedCommitSha, expectedArtifactSha256,
            requiredChecks, expectedDecisionAt, now);
        return new PromotionMachineEvidenceResult(artifactResult, gateResult);
    }
}

public readonly record struct PromotionMachineEvidenceResult(
    ArtifactIntegrityResult Artifact,
    GateCheckBundleResult? Gate)
{
    public bool ReadyForReview => Artifact == ArtifactIntegrityResult.Valid && Gate == GateCheckBundleResult.Valid;
}
