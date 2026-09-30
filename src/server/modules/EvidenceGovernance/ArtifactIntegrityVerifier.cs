using System.Security.Cryptography;

namespace EvidenceGovernance;

/// <summary>
/// Verifies the content digest and a detached signature against a trusted key supplied by the caller.
/// This is an internal primitive; it does not authorize artifact promotion.
/// </summary>
public static class ArtifactIntegrityVerifier
{
    public static ArtifactIntegrityResult Verify(
        ReadOnlySpan<byte> artifact,
        string? expectedSha256,
        ReadOnlySpan<byte> signature,
        RSA? trustedPublicKey)
    {
        if (artifact.IsEmpty || trustedPublicKey is null || signature.IsEmpty)
        {
            return ArtifactIntegrityResult.InvalidInput;
        }

        if (expectedSha256 is null || expectedSha256.Length != 64)
        {
            return ArtifactIntegrityResult.InvalidInput;
        }

        byte[] expectedDigest;
        try
        {
            expectedDigest = Convert.FromHexString(expectedSha256);
        }
        catch (FormatException)
        {
            return ArtifactIntegrityResult.InvalidInput;
        }
        Span<byte> actualDigest = stackalloc byte[32];
        SHA256.HashData(artifact, actualDigest);

        if (!CryptographicOperations.FixedTimeEquals(expectedDigest, actualDigest))
        {
            return ArtifactIntegrityResult.DigestMismatch;
        }

        try
        {
            return trustedPublicKey.VerifyData(
                artifact,
                signature,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pss)
                ? ArtifactIntegrityResult.Valid
                : ArtifactIntegrityResult.SignatureMismatch;
        }
        catch (CryptographicException)
        {
            return ArtifactIntegrityResult.SignatureMismatch;
        }
    }
}

public enum ArtifactIntegrityResult
{
    Valid,
    InvalidInput,
    DigestMismatch,
    SignatureMismatch
}
