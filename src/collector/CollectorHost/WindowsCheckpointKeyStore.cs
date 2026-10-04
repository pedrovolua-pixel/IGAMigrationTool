using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace CollectorHost;

public static class WindowsCheckpointKeyStore
{
    [SupportedOSPlatform("windows")]
    internal static void ProvisionNew(string protectedKeyPath, Guid scopeId)
    {
        ValidateScope(scopeId);
        if (!LocalPath.IsValid(protectedKeyPath))
        {
            throw new InvalidDataException("Protected key path is invalid.");
        }

        var directory = Path.GetDirectoryName(protectedKeyPath);
        if (directory is null)
        {
            throw new InvalidDataException("Protected key directory is invalid.");
        }

        WindowsProtectedConfig.ValidateProtectedStageDirectory(directory);
        var blob = ProtectNew(scopeId);
        var created = false;
        try
        {
            using (var stream = new FileStream(protectedKeyPath, FileMode.CreateNew, FileAccess.Write,
                       FileShare.None, 4096, FileOptions.WriteThrough))
            {
                created = true;
                stream.Write(blob);
                stream.Flush(true);
            }

            WindowsProtectedConfig.ValidateProtectedStageFile(protectedKeyPath);
        }
        catch
        {
            if (created && File.Exists(protectedKeyPath))
            {
                File.Delete(protectedKeyPath);
            }

            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(blob);
        }
    }

    [SupportedOSPlatform("windows")]
    public static byte[] Load(string protectedKeyPath, Guid scopeId)
    {
        ValidateScope(scopeId);
        var blob = WindowsProtectedConfig.ReadProtectedBytes(protectedKeyPath, 4096);
        try
        {
            var key = ProtectedData.Unprotect(blob, Entropy(scopeId), DataProtectionScope.LocalMachine);
            if (key.Length != 32)
            {
                CryptographicOperations.ZeroMemory(key);
                throw new CryptographicException("Checkpoint key length is invalid.");
            }

            return key;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(blob);
        }
    }

    [SupportedOSPlatform("windows")]
    public static byte[] ProtectNew(Guid scopeId)
    {
        ValidateScope(scopeId);
        var key = RandomNumberGenerator.GetBytes(32);
        try
        {
            return ProtectedData.Protect(key, Entropy(scopeId), DataProtectionScope.LocalMachine);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static byte[] Entropy(Guid scopeId) =>
        Encoding.UTF8.GetBytes("IGA-PILOT-CHECKPOINT-KEY-v1:" + scopeId.ToString("N"));

    private static void ValidateScope(Guid scopeId)
    {
        if (scopeId == Guid.Empty)
        {
            throw new ArgumentException("Scope must be nonempty.", nameof(scopeId));
        }
    }
}
