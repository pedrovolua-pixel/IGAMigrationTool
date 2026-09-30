using System.Security.Cryptography;

namespace CollectorSafety;

public sealed record EncryptedPayload(
    byte[] WrappedKey,
    byte[] Nonce,
    byte[] Tag,
    byte[] Ciphertext);

/// <summary>
/// Encrypts one already-minimized package payload with a random per-package AES-256-GCM
/// key wrapped by RSA-OAEP-SHA256. The exact package envelope, manifest,
/// signature and key identity are separate reviewed contracts.
/// </summary>
public static class OfflinePayloadEncryptionPrimitive
{
    public static EncryptedPayload Encrypt(
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> associatedData,
        RSA recipientPublicKey)
    {
        ArgumentNullException.ThrowIfNull(recipientPublicKey);
        if (associatedData.IsEmpty)
        {
            throw new ArgumentException("Associated metadata is required.", nameof(associatedData));
        }

        var key = RandomNumberGenerator.GetBytes(32);
        try
        {
            var wrappedKey = recipientPublicKey.Encrypt(key, RSAEncryptionPadding.OaepSHA256);
            var nonce = RandomNumberGenerator.GetBytes(12);
            var tag = new byte[16];
            var ciphertext = new byte[plaintext.Length];
            using var aes = new AesGcm(key, tag.Length);
            aes.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);
            return new EncryptedPayload(wrappedKey, nonce, tag, ciphertext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }
}
