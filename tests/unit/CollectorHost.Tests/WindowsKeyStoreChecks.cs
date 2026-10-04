using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using CollectorHost;

internal static class WindowsKeyStoreChecks
{
    [SupportedOSPlatform("windows")]
    public static int Run()
    {
        var count = 0;
        var directory = Path.Combine(Path.GetTempPath(), "iga-key-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var current = WindowsIdentity.GetCurrent().User ?? throw new Exception("Missing Windows identity.");
            var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(true, false);
            security.SetOwner(current);
            foreach (var principal in new[] { current, administrators, system })
            {
                security.AddAccessRule(new FileSystemAccessRule(principal, FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None, AccessControlType.Allow));
            }

            FileSystemAclExtensions.SetAccessControl(new DirectoryInfo(directory), security);
            var path = Path.Combine(directory, "key.blob");
            var scope = Guid.NewGuid();
            var broadDirectory = FileSystemAclExtensions.GetAccessControl(new DirectoryInfo(directory));
            broadDirectory.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
                FileSystemRights.Read, AccessControlType.Allow));
            FileSystemAclExtensions.SetAccessControl(new DirectoryInfo(directory), broadDirectory);
            Reject<InvalidOperationException>("broad key directory cannot provision", () =>
                WindowsCheckpointKeyStore.ProvisionNew(path, scope));
            Check("rejected key provisioning leaves no blob", !File.Exists(path));
            var restoredDirectory = FileSystemAclExtensions.GetAccessControl(new DirectoryInfo(directory));
            restoredDirectory.PurgeAccessRules(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null));
            FileSystemAclExtensions.SetAccessControl(new DirectoryInfo(directory), restoredDirectory);
            WindowsProtectedConfig.ValidateProtectedStageDirectory(directory);
            WindowsCheckpointKeyStore.ProvisionNew(path, scope);
            Check("create-once key provisioned", File.Exists(path));
            Reject<IOException>("existing key cannot be replaced", () =>
                WindowsCheckpointKeyStore.ProvisionNew(path, scope));
            var first = WindowsCheckpointKeyStore.Load(path, scope);
            var second = WindowsCheckpointKeyStore.Load(path, scope);
            Check("same key reloaded", first.Length == 32 && first.SequenceEqual(second));
            CryptographicOperations.ZeroMemory(first);
            CryptographicOperations.ZeroMemory(second);
            Reject<CryptographicException>("wrong scope", () => WindowsCheckpointKeyStore.Load(path, Guid.NewGuid()));

            var blob = File.ReadAllBytes(path);
            blob[^1] ^= 1;
            File.WriteAllBytes(path, blob);
            Reject<CryptographicException>("tampered protected key", () => WindowsCheckpointKeyStore.Load(path, scope));
            File.WriteAllBytes(path, WindowsCheckpointKeyStore.ProtectNew(scope));
            var file = new FileInfo(path);
            var fileSecurity = FileSystemAclExtensions.GetAccessControl(file);
            fileSecurity.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
                FileSystemRights.Write, AccessControlType.Allow));
            FileSystemAclExtensions.SetAccessControl(file, fileSecurity);
            Reject<InvalidOperationException>("unprotected key ACL", () => WindowsCheckpointKeyStore.Load(path, scope));
            return count;
        }
        finally
        {
            Directory.Delete(directory, true);
        }

        void Check(string name, bool okay)
        {
            if (!okay)
            {
                throw new Exception($"{name}: unexpected Windows key result.");
            }

            count++;
        }

        void Reject<T>(string name, Action action) where T : Exception
        {
            try
            {
                action();
                throw new Exception($"{name}: key operation was accepted.");
            }
            catch (T)
            {
                count++;
            }
        }
    }
}
