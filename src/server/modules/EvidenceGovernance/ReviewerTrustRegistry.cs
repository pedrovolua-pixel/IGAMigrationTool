using System.Security.Cryptography;

namespace EvidenceGovernance;

/// <summary>
/// A protected caller supplies this reviewer-key snapshot. Neither a reviewer
/// bundle nor its adjacent storage metadata can grant a reviewer role.
/// </summary>
public sealed class ReviewerTrustRegistry
{
    private readonly Dictionary<string, KeyEntry> _keys;

    public ReviewerTrustRegistry(IEnumerable<TrustedReviewerSigningKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        _keys = new Dictionary<string, KeyEntry>(StringComparer.Ordinal);
        var publicKeyDigests = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            if (key is null || !GateCheckBundleRules.IsCheckId(key.KeyId) ||
                !GateCheckBundleRules.IsCheckId(key.ReviewerId) ||
                !GateCheckBundleRules.IsCheckId(key.Role) ||
                key.SubjectPublicKeyInfo is null || key.SubjectPublicKeyInfo.Length == 0 ||
                key.AllowedScopes is null || key.AllowedScopes.Count == 0 ||
                key.AllowedScopes.Any(scope => !GateCheckBundleRules.IsCheckId(scope)) ||
                key.AllowedScopes.Count != key.AllowedScopes.Distinct(StringComparer.Ordinal).Count() ||
                key.ValidFrom.Offset != TimeSpan.Zero || key.ValidUntilExclusive.Offset != TimeSpan.Zero ||
                key.ValidFrom >= key.ValidUntilExclusive ||
                key.RevokedAt is { } revokedAt && revokedAt.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException("Invalid reviewer trust entry.", nameof(keys));
            }

            var publicKeyBytes = (byte[])key.SubjectPublicKeyInfo.Clone();
            using var rsa = RSA.Create();
            try
            {
                rsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out var bytesRead);
                if (bytesRead != publicKeyBytes.Length || rsa.KeySize < 2048)
                {
                    throw new ArgumentException("Invalid reviewer public key.", nameof(keys));
                }

                publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
            }
            catch (CryptographicException exception)
            {
                throw new ArgumentException("Invalid reviewer public key.", nameof(keys), exception);
            }

            if (!_keys.TryAdd(key.KeyId, new KeyEntry(publicKeyBytes,
                    key.ReviewerId, key.Role, key.AllowedScopes.ToHashSet(StringComparer.Ordinal),
                    key.ValidFrom, key.ValidUntilExclusive, key.RevokedAt)) ||
                !publicKeyDigests.Add(Convert.ToHexString(SHA256.HashData(publicKeyBytes))))
            {
                throw new ArgumentException("Duplicate reviewer trust entry or public key.", nameof(keys));
            }
        }
    }

    public ReviewerVerification VerifyApproval(string? keyId, ReadOnlySpan<byte> bundle,
        string? bundleSha256, ReadOnlySpan<byte> signature,
        string? expectedArtifactSha256, string? expectedScopeId,
        DateTimeOffset expectedDecisionAt, DateTimeOffset now)
    {
        if (!GateCheckBundleRules.IsCheckId(keyId) ||
            !GateCheckBundleRules.IsHex(expectedArtifactSha256, 64) ||
            !GateCheckBundleRules.IsCheckId(expectedScopeId) ||
            expectedDecisionAt.Offset != TimeSpan.Zero || now.Offset != TimeSpan.Zero)
        {
            return new ReviewerVerification(ReviewerDecisionResult.InvalidInput, null);
        }

        if (!_keys.TryGetValue(keyId!, out var key) ||
            !key.AllowedScopes.Contains(expectedScopeId!) ||
            expectedDecisionAt < key.ValidFrom || expectedDecisionAt >= key.ValidUntilExclusive ||
            key.RevokedAt is { } revokedAt && now >= revokedAt)
        {
            return new ReviewerVerification(ReviewerDecisionResult.Untrusted, null);
        }

        using var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(key.SubjectPublicKeyInfo, out _);
        var result = ReviewerDecisionBundle.Verify(bundle, bundleSha256, signature, rsa,
            expectedArtifactSha256, expectedScopeId, key.ReviewerId, key.Role,
            expectedDecisionAt, now);
        var proof = result == ReviewerDecisionResult.ValidApproval
            ? new VerifiedReviewerApproval(expectedArtifactSha256!, expectedScopeId!,
                key.ReviewerId, key.Role, expectedDecisionAt, now)
            : null;
        return new ReviewerVerification(result, proof);
    }

    private sealed record KeyEntry(byte[] SubjectPublicKeyInfo, string ReviewerId,
        string Role, HashSet<string> AllowedScopes, DateTimeOffset ValidFrom,
        DateTimeOffset ValidUntilExclusive, DateTimeOffset? RevokedAt);
}

public sealed record TrustedReviewerSigningKey(string KeyId, byte[] SubjectPublicKeyInfo,
    string ReviewerId, string Role, IReadOnlyCollection<string> AllowedScopes,
    DateTimeOffset ValidFrom, DateTimeOffset ValidUntilExclusive, DateTimeOffset? RevokedAt);

public sealed class VerifiedReviewerApproval
{
    internal VerifiedReviewerApproval(string artifactSha256, string scopeId, string reviewerId,
        string role, DateTimeOffset decisionAt, DateTimeOffset verifiedAt)
    {
        ArtifactSha256 = artifactSha256;
        ScopeId = scopeId;
        ReviewerId = reviewerId;
        Role = role;
        DecisionAt = decisionAt;
        VerifiedAt = verifiedAt;
    }

    public string ArtifactSha256 { get; }
    public string ScopeId { get; }
    public string ReviewerId { get; }
    public string Role { get; }
    public DateTimeOffset DecisionAt { get; }
    public DateTimeOffset VerifiedAt { get; }
}

public readonly record struct ReviewerVerification(ReviewerDecisionResult Result,
    VerifiedReviewerApproval? Approval);
