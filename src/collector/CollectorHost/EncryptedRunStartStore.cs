using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using CollectorSafety;

namespace CollectorHost;

// Internal one-extraction record. The caller must keep this path stable for every retry.
internal static class EncryptedRunStartStore
{
    private const int MaxPlaintextBytes = 4096;
    private static readonly byte[] Magic = "IGR1"u8.ToArray();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        MaxDepth = 12,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true
    };

    private sealed record RunStartDocument(int SchemaVersion, PageCheckpointContext Context,
        DateTimeOffset StartedAtUtc);

    internal static DateTimeOffset LoadOrCreate(string path, PageCheckpointContext context,
        DateTimeOffset proposedStart, ReadOnlySpan<byte> key)
    {
        ValidateLocation(path, context, key);
        if (File.Exists(path)) return Load(path, context, key);
        if (proposedStart < DateTimeOffset.UnixEpoch || proposedStart > DateTimeOffset.UtcNow)
        {
            throw new InvalidDataException("Extraction start is invalid.");
        }

        var directory = Path.GetDirectoryName(path)!;
        if (Directory.EnumerateFiles(directory, "*.stage").Any())
        {
            throw new InvalidDataException("Staged pages exist without a run-start record.");
        }

        var plaintext = JsonSerializer.SerializeToUtf8Bytes(new RunStartDocument(1, context,
            proposedStart.ToUniversalTime()), JsonOptions);
        try
        {
            if (plaintext.Length > MaxPlaintextBytes)
            {
                throw new InvalidDataException("Run-start record exceeds its size limit.");
            }

            var nonce = RandomNumberGenerator.GetBytes(12);
            var tag = new byte[16];
            var ciphertext = new byte[plaintext.Length];
            try
            {
                using (var aes = new AesGcm(key, tag.Length))
                {
                    aes.Encrypt(nonce, plaintext, ciphertext, tag, AssociatedData(context));
                }

                try
                {
                    using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
                               FileShare.None, 4096, FileOptions.WriteThrough))
                    {
                        stream.Write(Magic);
                        stream.Write(nonce);
                        stream.Write(tag);
                        stream.Write(ciphertext);
                        stream.Flush(true);
                    }

                    ValidateWindowsFile(path);
                }
                catch (IOException) when (File.Exists(path))
                {
                    return Load(path, context, key);
                }

                return proposedStart.ToUniversalTime();
            }
            finally
            {
                CryptographicOperations.ZeroMemory(ciphertext);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    internal static DateTimeOffset Load(string path, PageCheckpointContext context, ReadOnlySpan<byte> key)
    {
        ValidateLocation(path, context, key);
        if (!File.Exists(path)) throw new InvalidDataException("Run-start record is absent.");
        ValidateWindowsFile(path);
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Run-start path is a reparse point.");
        }

        var length = new FileInfo(path).Length;
        if (length is < 33 or > MaxPlaintextBytes + 32)
        {
            throw new InvalidDataException("Run-start length is invalid.");
        }

        var envelope = File.ReadAllBytes(path);
        if (envelope.Length != length || !envelope.AsSpan(0, 4).SequenceEqual(Magic))
        {
            throw new InvalidDataException("Run-start record changed or has an unsupported version.");
        }

        var plaintext = new byte[envelope.Length - 32];
        try
        {
            using (var aes = new AesGcm(key, 16))
            {
                aes.Decrypt(envelope.AsSpan(4, 12), envelope.AsSpan(32),
                    envelope.AsSpan(16, 16), plaintext, AssociatedData(context));
            }

            var document = JsonSerializer.Deserialize<RunStartDocument>(plaintext, JsonOptions);
            if (document is null || document.SchemaVersion != 1 || document.Context != context ||
                document.StartedAtUtc < DateTimeOffset.UnixEpoch ||
                document.StartedAtUtc > DateTimeOffset.UtcNow)
            {
                throw new InvalidDataException("Run-start content is invalid.");
            }

            return document.StartedAtUtc;
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("Run-start content is malformed.", error);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static void ValidateLocation(string path, PageCheckpointContext context, ReadOnlySpan<byte> key)
    {
        if (key.Length != 32) throw new CryptographicException("A 256-bit run-start key is required.");
        if (PageCheckpointVerifier.Evaluate(context, [],
                new PageCheckpoint(context, "run-start", new string('0', 64), 0, false)) !=
            PageCheckpointDecision.NewPage || string.IsNullOrWhiteSpace(path) ||
            !Path.IsPathFullyQualified(path) || (OperatingSystem.IsWindows() && !LocalPath.IsValid(path)))
        {
            throw new InvalidDataException("Run-start location or context is invalid.");
        }

        var directory = Path.GetDirectoryName(path);
        if (directory is null || !Directory.Exists(directory) ||
            (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Run-start directory is invalid.");
        }

        if (!OperatingSystem.IsWindows()) return;
        try
        {
            WindowsProtectedConfig.ValidateProtectedStageDirectory(directory);
        }
        catch (InvalidOperationException error)
        {
            throw new InvalidDataException("Run-start directory protection is invalid.", error);
        }
    }

    private static void ValidateWindowsFile(string path)
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            WindowsProtectedConfig.ValidateProtectedStageFile(path);
        }
        catch (InvalidOperationException error)
        {
            throw new InvalidDataException("Run-start file protection is invalid.", error);
        }
    }

    private static byte[] AssociatedData(PageCheckpointContext context) =>
        JsonSerializer.SerializeToUtf8Bytes(new { Domain = "IGR1", Context = context });
}
