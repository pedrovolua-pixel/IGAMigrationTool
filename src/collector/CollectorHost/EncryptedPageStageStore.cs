using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CollectorSafety;

namespace CollectorHost;

// Internal persistence primitive for one extraction run only. Windows path/ACL checks
// guard each operation, but a reviewed adapter must provision a distinct protected run
// directory and manage the key and aggregate queue limits before customer use.
internal static class EncryptedPageStageStore
{
    private const int MaxPlaintextBytes = 1024 * 1024;
    private static readonly byte[] Magic = "IGS2"u8.ToArray();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        MaxDepth = 16,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true
    };

    internal enum StageResult { NewPage, IdempotentReplay }

    internal sealed record StagedPage(int RowCount, bool IsTerminal, string Digest,
        IReadOnlyList<MinimizedField> Fields);
    private sealed record StoredField(string CategoryId, string FieldId, FieldDisposition Disposition,
        string? IncludedValue);
    private sealed record StageDocument(int SchemaVersion, PageCheckpointContext Context, string Boundary,
        int RowCount, bool IsTerminal, string Digest, StoredField[] Fields);

    internal static StageResult Stage(string directory, PageCheckpointContext context, string boundary,
        int rowCount, bool isTerminal, string digest, IReadOnlyList<MinimizedField> fields,
        long maxLocalBytes, ReadOnlySpan<byte> key)
    {
        ValidateInput(directory, context, boundary, rowCount, digest, fields, key);
        if (maxLocalBytes <= 0)
        {
            throw new InvalidDataException("Local staging byte limit is invalid.");
        }
        if (!string.Equals(MinimizedPageDigest.Compute(boundary, rowCount, isTerminal, fields), digest,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Staged page digest does not match minimized content.");
        }

        var path = FilePath(directory, context, boundary, key);
        var prior = LoadPath(path, context, boundary, key);
        if (prior is not null)
        {
            if (!string.Equals(prior.Digest, digest, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Staged page conflicts with its prior digest.");
            }

            return StageResult.IdempotentReplay;
        }

        var document = new StageDocument(2, context, boundary, rowCount, isTerminal, digest,
            [.. fields.Select(field => new StoredField(field.Key!.CategoryId, field.Key.FieldId,
                field.Disposition, field.IncludedValue))]);
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
        try
        {
            if (plaintext.Length > MaxPlaintextBytes)
            {
                throw new InvalidDataException("Staged page exceeds the per-page size limit.");
            }

            LocalRunDirectoryCapacity.Validate(directory, maxLocalBytes, plaintext.Length + 32L);

            var nonce = RandomNumberGenerator.GetBytes(12);
            var tag = new byte[16];
            var ciphertext = new byte[plaintext.Length];
            try
            {
                using (var aes = new AesGcm(key, tag.Length))
                {
                    aes.Encrypt(nonce, plaintext, ciphertext, tag, AssociatedData(context, boundary));
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

                    ValidateWindowsFile(temp);

                    // Never overwrite a prior page, including one written by another process.
                    File.Move(temp, path);
                }
                catch (IOException) when (File.Exists(path))
                {
                    var concurrent = LoadPath(path, context, boundary, key);
                    if (concurrent is null || !string.Equals(concurrent.Digest, digest,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException("Concurrent staged page has conflicting content.");
                    }

                    return StageResult.IdempotentReplay;
                }
                finally
                {
                    if (File.Exists(temp)) File.Delete(temp);
                }

                return StageResult.NewPage;
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

    internal static StagedPage? Load(string directory, PageCheckpointContext context, string boundary,
        ReadOnlySpan<byte> key)
    {
        ValidateLocation(directory, key);
        ValidateCheckpoint(context, boundary, new string('0', 64));
        return LoadPath(FilePath(directory, context, boundary, key), context, boundary, key);
    }

    private static StagedPage? LoadPath(string path, PageCheckpointContext context, string boundary,
        ReadOnlySpan<byte> key)
    {
        if (!File.Exists(path)) return null;
        ValidateWindowsFile(path);
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Staged page path is a reparse point.");
        }

        var length = new FileInfo(path).Length;
        if (length is < 33 or > MaxPlaintextBytes + 32)
        {
            throw new InvalidDataException("Staged page length is invalid.");
        }

        var envelope = File.ReadAllBytes(path);
        if (envelope.Length != length || !envelope.AsSpan(0, 4).SequenceEqual(Magic))
        {
            throw new InvalidDataException("Staged page changed or has an unsupported version.");
        }

        var plaintext = new byte[envelope.Length - 32];
        try
        {
            using (var aes = new AesGcm(key, 16))
            {
                aes.Decrypt(envelope.AsSpan(4, 12), envelope.AsSpan(32),
                    envelope.AsSpan(16, 16), plaintext, AssociatedData(context, boundary));
            }

            var document = JsonSerializer.Deserialize<StageDocument>(plaintext, JsonOptions);
            if (document is null || document.SchemaVersion != 2 || document.Context != context ||
                document.Boundary != boundary || document.Fields is null)
            {
                throw new InvalidDataException("Staged page context is incompatible.");
            }

            if (document.Fields.Any(field => field is null))
            {
                throw new InvalidDataException("Staged page contains a missing field.");
            }
            var fields = document.Fields.Select(field =>
                new MinimizedField(new FieldKey(field.CategoryId, field.FieldId), field.Disposition,
                    field.IncludedValue)).ToArray();
            ValidateInput(Path.GetDirectoryName(path)!, context, boundary, document.RowCount,
                document.Digest, fields, key);
            if (!string.Equals(MinimizedPageDigest.Compute(boundary, document.RowCount,
                    document.IsTerminal, fields),
                    document.Digest, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Staged page digest is inconsistent.");
            }

            return new StagedPage(document.RowCount, document.IsTerminal, document.Digest, fields);
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("Staged page is malformed.", error);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static void ValidateInput(string directory, PageCheckpointContext context, string boundary,
        int rowCount, string digest, IReadOnlyList<MinimizedField> fields, ReadOnlySpan<byte> key)
    {
        ValidateLocation(directory, key);
        ValidateCheckpoint(context, boundary, digest);
        if (rowCount < 0 || fields is null || fields.Count > 4096 || fields.Any(field =>
                field is null || field.Key is null || !ValidId(field.Key.CategoryId) || !ValidId(field.Key.FieldId) ||
                field.Disposition is not (FieldDisposition.Included or FieldDisposition.Redacted or
                    FieldDisposition.Excluded) ||
                (field.Disposition != FieldDisposition.Included && field.IncludedValue is not null)))
        {
            throw new InvalidDataException("Staged page contains an invalid or prohibited field.");
        }
    }

    private static void ValidateCheckpoint(PageCheckpointContext context, string boundary, string digest)
    {
        if (PageCheckpointVerifier.Evaluate(context, [], new PageCheckpoint(context, boundary, digest, 0, false)) !=
            PageCheckpointDecision.NewPage || boundary.Length > 1024)
        {
            throw new InvalidDataException("Staged page checkpoint identity is invalid.");
        }
    }

    private static void ValidateLocation(string directory, ReadOnlySpan<byte> key)
    {
        if (key.Length != 32) throw new CryptographicException("A 256-bit stage key is required.");
        if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory) ||
            !Directory.Exists(directory) || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Staging directory is invalid.");
        }

        if (OperatingSystem.IsWindows())
        {
            try
            {
                WindowsProtectedConfig.ValidateProtectedStageDirectory(directory);
            }
            catch (InvalidOperationException error)
            {
                throw new InvalidDataException("Staging directory protection is invalid.", error);
            }
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
            throw new InvalidDataException("Staged page file protection is invalid.", error);
        }
    }

    private static bool ValidId(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 128 && !value.Any(char.IsControl);

    private static string FilePath(string directory, PageCheckpointContext context, string boundary,
        ReadOnlySpan<byte> key) =>
        Path.Combine(directory, Convert.ToHexString(HMACSHA256.HashData(key, AssociatedData(context, boundary)))
            .ToLowerInvariant() + ".stage");

    private static byte[] AssociatedData(PageCheckpointContext context, string boundary) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Domain = "IGS2", Context = context, Boundary = boundary }));
}

internal static class MinimizedPageDigest
{
    internal static string Compute(string boundary, int rowCount, bool isTerminal,
        IReadOnlyList<MinimizedField> fields)
    {
        var serialized = JsonSerializer.SerializeToUtf8Bytes(new
        {
            Boundary = boundary,
            RowCount = rowCount,
            IsTerminal = isTerminal,
            Fields = fields.Select(field => new
            {
                field.Key?.CategoryId,
                field.Key?.FieldId,
                field.Disposition,
                field.IncludedValue
            })
        });
        try
        {
            return Convert.ToHexString(SHA256.HashData(serialized)).ToLowerInvariant();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(serialized);
        }
    }
}
