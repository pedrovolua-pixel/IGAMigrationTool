using System.Security.Cryptography;

namespace EvidenceGovernance;

/// <summary>
/// An in-memory snapshot of signing trust supplied by protected configuration.
/// Bundle content never registers, scopes, or revokes a trusted key.
/// </summary>
public sealed class GateSigningTrustRegistry
{
    private readonly Dictionary<string, KeyEntry> _keys;

    public GateSigningTrustRegistry(IEnumerable<TrustedGateSigningKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        _keys = new Dictionary<string, KeyEntry>(StringComparer.Ordinal);
        var publicKeyDigests = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            if (key is null || !GateCheckBundleRules.IsCheckId(key.KeyId) ||
                key.SubjectPublicKeyInfo is null || key.SubjectPublicKeyInfo.Length == 0 ||
                key.AllowedGates is null || key.AllowedGates.Count == 0 ||
                key.AllowedGates.Any(gate => !GateCheckBundleRules.IsGate(gate)) ||
                key.AllowedGates.Count != key.AllowedGates.Distinct(StringComparer.Ordinal).Count() ||
                key.ValidFrom.Offset != TimeSpan.Zero || key.ValidUntilExclusive.Offset != TimeSpan.Zero ||
                key.ValidFrom >= key.ValidUntilExclusive ||
                key.RevokedAt is { } revokedAt && revokedAt.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException("Invalid signing trust entry.", nameof(keys));
            }

            var publicKeyBytes = (byte[])key.SubjectPublicKeyInfo.Clone();
            using var rsa = RSA.Create();
            try
            {
                rsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out var bytesRead);
                if (bytesRead != publicKeyBytes.Length || rsa.KeySize < 2048)
                {
                    throw new ArgumentException("Invalid signing public key.", nameof(keys));
                }

                publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
            }
            catch (CryptographicException exception)
            {
                throw new ArgumentException("Invalid signing public key.", nameof(keys), exception);
            }

            var keyDigest = Convert.ToHexString(SHA256.HashData(publicKeyBytes));
            if (!_keys.TryAdd(key.KeyId, new KeyEntry(publicKeyBytes,
                    key.AllowedGates.ToHashSet(StringComparer.Ordinal), key.ValidFrom,
                    key.ValidUntilExclusive, key.RevokedAt)) ||
                !publicKeyDigests.Add(keyDigest))
            {
                throw new ArgumentException("Duplicate signing trust entry or public key.", nameof(keys));
            }
        }
    }

    public GateCheckBundleResult VerifyBundle(
        string? keyId,
        ReadOnlySpan<byte> bundle,
        string? bundleSha256,
        ReadOnlySpan<byte> signature,
        string? expectedGate,
        string? expectedCommitSha,
        string? expectedArtifactSha256,
        IReadOnlyCollection<string>? requiredChecks,
        DateTimeOffset expectedDecisionAt,
        DateTimeOffset now)
    {
        if (!GateCheckBundleRules.IsCheckId(keyId) ||
            expectedDecisionAt.Offset != TimeSpan.Zero || now.Offset != TimeSpan.Zero)
        {
            return GateCheckBundleResult.InvalidInput;
        }

        if (!_keys.TryGetValue(keyId!, out var key) ||
            !key.AllowedGates.Contains(expectedGate ?? "") ||
            expectedDecisionAt < key.ValidFrom || expectedDecisionAt >= key.ValidUntilExclusive ||
            key.RevokedAt is { } revokedAt && now >= revokedAt)
        {
            return GateCheckBundleResult.Untrusted;
        }

        using var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(key.SubjectPublicKeyInfo, out _);
        return GateCheckBundleVerifier.Verify(bundle, bundleSha256, signature, rsa,
            expectedGate, expectedCommitSha, expectedArtifactSha256, requiredChecks,
            expectedDecisionAt, now);
    }

    private sealed record KeyEntry(
        byte[] SubjectPublicKeyInfo,
        HashSet<string> AllowedGates,
        DateTimeOffset ValidFrom,
        DateTimeOffset ValidUntilExclusive,
        DateTimeOffset? RevokedAt);
}

public sealed record TrustedGateSigningKey(
    string KeyId,
    byte[] SubjectPublicKeyInfo,
    IReadOnlyCollection<string> AllowedGates,
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidUntilExclusive,
    DateTimeOffset? RevokedAt);
