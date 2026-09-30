using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CollectorSafety;

namespace CollectorHost;

public static class EncryptedCheckpointStore
{
    private const int MaxPages = 4096;
    private const int MaxPlaintextBytes = 1024 * 1024;
    private static readonly byte[] Magic = "IGC1"u8.ToArray();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        MaxDepth = 16,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static IReadOnlyList<PageCheckpoint>? Load(
        string path, PageCheckpointContext expectedContext, ReadOnlySpan<byte> key)
    {
        ValidateKey(key);
        ArgumentNullException.ThrowIfNull(expectedContext);
        ValidateContext(expectedContext);
        if (!File.Exists(path))
        {
            return null;
        }

        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Checkpoint path is a reparse point.");
        }

        var file = new FileInfo(path);
        if (file.Length is < 33 or > MaxPlaintextBytes + 32)
        {
            throw new InvalidDataException("Checkpoint length is invalid.");
        }

        var envelope = File.ReadAllBytes(path);
        if (envelope.Length is < 33 or > MaxPlaintextBytes + 32)
        {
            throw new InvalidDataException("Checkpoint length changed during read.");
        }

        if (!envelope.AsSpan(0, 4).SequenceEqual(Magic))
        {
            throw new InvalidDataException("Checkpoint version is unsupported.");
        }

        var plaintext = new byte[envelope.Length - 32];
        try
        {
            using (var aes = new AesGcm(key, 16))
            {
                aes.Decrypt(envelope.AsSpan(4, 12), envelope.AsSpan(32),
                    envelope.AsSpan(16, 16), plaintext, ContextBytes(expectedContext));
            }

            var document = JsonSerializer.Deserialize<CheckpointDocument>(plaintext, JsonOptions);
            if (document is null || document.SchemaVersion != 1 || document.Context != expectedContext ||
                document.Pages is null)
            {
                throw new InvalidDataException("Checkpoint context is incompatible.");
            }

            ValidatePages(expectedContext, document.Pages);
            return document.Pages;
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("Checkpoint data is malformed.", error);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public static void Save(
        string path, PageCheckpointContext context, IReadOnlyList<PageCheckpoint> pages, ReadOnlySpan<byte> key)
    {
        ValidateKey(key);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(pages);
        ValidateContext(context);
        ValidatePages(context, pages);
        var previous = Load(path, context, key);
        if (previous is not null &&
            (previous.Count > pages.Count || !previous.SequenceEqual(pages.Take(previous.Count))))
        {
            throw new InvalidDataException("Completed checkpoint pages cannot be rewritten.");
        }

        var plaintext = JsonSerializer.SerializeToUtf8Bytes(new CheckpointDocument(1, context, [.. pages]), JsonOptions);
        if (plaintext.Length > MaxPlaintextBytes)
        {
            throw new InvalidDataException("Checkpoint exceeds the local size limit.");
        }

        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var ciphertext = new byte[plaintext.Length];
        try
        {
            using (var aes = new AesGcm(key, tag.Length))
            {
                aes.Encrypt(nonce, plaintext, ciphertext, tag, ContextBytes(context));
            }

            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write,
                           FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(Magic);
                    stream.Write(nonce);
                    stream.Write(tag);
                    stream.Write(ciphertext);
                    stream.Flush(true);
                }

                File.Move(temp, path, true);
            }
            finally
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(ciphertext);
        }
    }

    private static void ValidatePages(PageCheckpointContext context, IReadOnlyList<PageCheckpoint> pages)
    {
        if (pages.Count > MaxPages)
        {
            throw new InvalidDataException("Checkpoint page count exceeds the local limit.");
        }

        var accepted = new List<PageCheckpoint>();
        foreach (var page in pages)
        {
            if (PageCheckpointVerifier.Evaluate(context, accepted, page) != PageCheckpointDecision.NewPage)
            {
                throw new InvalidDataException("Checkpoint pages are incompatible or duplicated.");
            }

            accepted.Add(page);
        }
    }

    private static void ValidateContext(PageCheckpointContext context)
    {
        if (string.IsNullOrWhiteSpace(context.QueryId) ||
            string.IsNullOrWhiteSpace(context.QueryVersion) ||
            string.IsNullOrWhiteSpace(context.ExactBuild) ||
            string.IsNullOrWhiteSpace(context.ScopeId) ||
            string.IsNullOrWhiteSpace(context.PolicyVersion) ||
            string.IsNullOrWhiteSpace(context.OrderingKey))
        {
            throw new InvalidDataException("Checkpoint context is incomplete.");
        }
    }

    private static byte[] ContextBytes(PageCheckpointContext context) =>
        Encoding.UTF8.GetBytes(string.Join('\0', ["IGC1", context.QueryId, context.QueryVersion,
            context.ExactBuild, context.ScopeId, context.PolicyVersion, context.OrderingKey]));

    private static void ValidateKey(ReadOnlySpan<byte> key)
    {
        if (key.Length != 32)
        {
            throw new ArgumentException("Checkpoint key must have 32 bytes.", nameof(key));
        }
    }

    private sealed record CheckpointDocument(int SchemaVersion, PageCheckpointContext Context,
        PageCheckpoint[] Pages);
}
