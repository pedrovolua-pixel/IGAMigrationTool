using System.Security.Cryptography;

namespace EvidenceGovernance;

/// <summary>
/// Protected configuration supplies this key/owner snapshot. A submitted
/// override cannot register its own signing key or owner identity.
/// </summary>
public sealed class OwnerOverrideTrustRegistry
{
    private readonly Dictionary<string, KeyEntry> _keys;

    public OwnerOverrideTrustRegistry(IEnumerable<TrustedOwnerOverrideKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        _keys = new Dictionary<string, KeyEntry>(StringComparer.Ordinal);
        var publicKeyDigests = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            if (key is null || !GateCheckBundleRules.IsCheckId(key.KeyId) ||
                !GateCheckBundleRules.IsCheckId(key.OwnerId) ||
                key.SubjectPublicKeyInfo is null || key.SubjectPublicKeyInfo.Length == 0 ||
                key.AllowedScopes is null || key.AllowedScopes.Count == 0 ||
                key.AllowedScopes.Any(scope => !GateCheckBundleRules.IsCheckId(scope)) ||
                key.AllowedScopes.Count != key.AllowedScopes.Distinct(StringComparer.Ordinal).Count() ||
                key.ValidFrom.Offset != TimeSpan.Zero || key.ValidUntilExclusive.Offset != TimeSpan.Zero ||
                key.ValidFrom >= key.ValidUntilExclusive ||
                key.RevokedAt is { } revokedAt && revokedAt.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException("Invalid owner override trust entry.", nameof(keys));
            }

            var publicKeyBytes = (byte[])key.SubjectPublicKeyInfo.Clone();
            using var rsa = RSA.Create();
            try
            {
                rsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out var bytesRead);
                if (bytesRead != publicKeyBytes.Length || rsa.KeySize < 2048)
                {
                    throw new ArgumentException("Invalid owner override public key.", nameof(keys));
                }

                publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
            }
            catch (CryptographicException exception)
            {
                throw new ArgumentException("Invalid owner override public key.", nameof(keys), exception);
            }

            if (!_keys.TryAdd(key.KeyId, new KeyEntry(publicKeyBytes, key.OwnerId,
                    key.AllowedScopes.ToHashSet(StringComparer.Ordinal), key.ValidFrom,
                    key.ValidUntilExclusive, key.RevokedAt)) ||
                !publicKeyDigests.Add(Convert.ToHexString(SHA256.HashData(publicKeyBytes))))
            {
                throw new ArgumentException("Duplicate owner override key or public key.", nameof(keys));
            }
        }
    }

    public OwnerOverrideResult VerifyOverride(string? keyId, ReadOnlySpan<byte> bundle,
        string? bundleSha256, ReadOnlySpan<byte> signature, ReadOnlySpan<byte> rationale,
        OwnerOverrideMetadata? expected, DateTimeOffset trustedPilotEndsAt,
        DateTimeOffset now)
    {
        if (!GateCheckBundleRules.IsCheckId(keyId) || expected is null ||
            now.Offset != TimeSpan.Zero)
        {
            return OwnerOverrideResult.InvalidInput;
        }

        if (!_keys.TryGetValue(keyId!, out var key) ||
            key.OwnerId != expected.OwnerId || !key.AllowedScopes.Contains(expected.ScopeId) ||
            expected.DecisionAt < key.ValidFrom || expected.DecisionAt >= key.ValidUntilExclusive ||
            key.RevokedAt is { } revokedAt && now >= revokedAt)
        {
            return OwnerOverrideResult.Untrusted;
        }

        using var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(key.SubjectPublicKeyInfo, out _);
        return PilotOwnerOverrideBundle.Verify(bundle, bundleSha256, signature, rsa,
            rationale, expected, trustedPilotEndsAt, now);
    }

    private sealed record KeyEntry(byte[] SubjectPublicKeyInfo, string OwnerId,
        HashSet<string> AllowedScopes, DateTimeOffset ValidFrom,
        DateTimeOffset ValidUntilExclusive, DateTimeOffset? RevokedAt);
}

public sealed record TrustedOwnerOverrideKey(string KeyId, byte[] SubjectPublicKeyInfo,
    string OwnerId, IReadOnlyCollection<string> AllowedScopes,
    DateTimeOffset ValidFrom, DateTimeOffset ValidUntilExclusive, DateTimeOffset? RevokedAt);
