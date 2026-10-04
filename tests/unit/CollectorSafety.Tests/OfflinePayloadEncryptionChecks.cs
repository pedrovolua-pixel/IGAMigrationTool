using System.Security.Cryptography;
using System.Text;
using CollectorSafety;

internal static class OfflinePayloadEncryptionChecks
{
    internal static int Run()
    {
        var payload = Encoding.UTF8.GetBytes("synthetic permitted evidence");
        var manifestBinding = Encoding.UTF8.GetBytes("synthetic manifest binding");
        using var recipient = RSA.Create(2048);
        using var wrongRecipient = RSA.Create(2048);
        var first = OfflinePayloadEncryptionPrimitive.Encrypt(payload, manifestBinding, recipient);
        var second = OfflinePayloadEncryptionPrimitive.Encrypt(payload, manifestBinding, recipient);
        var count = 0;

        Require(first.Nonce.Length == 12 && first.Tag.Length == 16 &&
                first.Ciphertext.Length == payload.Length && first.WrappedKey.Length > 0,
            "expected primitive sizes");
        Require(Decrypt(first, manifestBinding, recipient).SequenceEqual(payload),
            "round trip through platform test key");
        Require(!first.Nonce.SequenceEqual(second.Nonce) &&
                !first.WrappedKey.SequenceEqual(second.WrappedKey) &&
                !first.Ciphertext.SequenceEqual(second.Ciphertext),
            "fresh key and nonce per package payload");

        ExpectCryptographicFailure("wrong recipient key", () =>
            Decrypt(first, manifestBinding, wrongRecipient));
        ExpectCryptographicFailure("changed associated metadata", () =>
            Decrypt(first, Encoding.UTF8.GetBytes("other binding"), recipient));
        ExpectCryptographicFailure("changed ciphertext", () =>
            Decrypt(first with { Ciphertext = ChangeFirstByte(first.Ciphertext) }, manifestBinding, recipient));
        ExpectCryptographicFailure("changed tag", () =>
            Decrypt(first with { Tag = ChangeFirstByte(first.Tag) }, manifestBinding, recipient));
        ExpectCryptographicFailure("changed nonce", () =>
            Decrypt(first with { Nonce = ChangeFirstByte(first.Nonce) }, manifestBinding, recipient));
        ExpectCryptographicFailure("changed wrapped key", () =>
            Decrypt(first with { WrappedKey = ChangeFirstByte(first.WrappedKey) }, manifestBinding, recipient));

        try
        {
            OfflinePayloadEncryptionPrimitive.Encrypt(payload, [], recipient);
            throw new Exception("Expected empty associated-data rejection.");
        }
        catch (ArgumentException)
        {
            count++;
        }

        return count;

        void Require(bool condition, string name)
        {
            if (!condition)
            {
                throw new Exception($"Offline encryption case failed: {name}.");
            }

            count++;
        }

        void ExpectCryptographicFailure(string name, Func<byte[]> action)
        {
            try
            {
                action();
                throw new Exception($"Offline encryption case failed: {name} was accepted.");
            }
            catch (CryptographicException)
            {
                count++;
            }
        }
    }

    private static byte[] Decrypt(EncryptedPayload part, byte[] associatedData, RSA recipient)
    {
        var key = recipient.Decrypt(part.WrappedKey, RSAEncryptionPadding.OaepSHA256);
        try
        {
            var plaintext = new byte[part.Ciphertext.Length];
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(part.Nonce, part.Ciphertext, part.Tag, plaintext, associatedData);
            return plaintext;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static byte[] ChangeFirstByte(byte[] source)
    {
        var changed = (byte[])source.Clone();
        changed[0] ^= 0x01;
        return changed;
    }
}
