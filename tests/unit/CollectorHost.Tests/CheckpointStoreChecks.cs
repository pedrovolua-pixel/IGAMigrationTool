using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using CollectorHost;
using CollectorSafety;

internal static class CheckpointStoreChecks
{
    public static int Run()
    {
        var count = 0;
        var directory = Path.Combine(Path.GetTempPath(), "iga-checkpoint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        if (OperatingSystem.IsWindows()) WindowsStageTestDirectory.Protect(directory);
        try
        {
            var path = Path.Combine(directory, "checkpoint.enc");
            var key = RandomNumberGenerator.GetBytes(32);
            var context = new PageCheckpointContext("query", "1", "10.0.0.1", "scope", "policy", "uid");
            var first = new PageCheckpoint(context, "page-1", new string('a', 64), 2, false);
            var second = new PageCheckpoint(context, "page-2", new string('b', 64), 3, true);
            const long localByteCap = 100_000;
            Reject<InvalidDataException>("new checkpoint obeys local byte cap", () =>
                EncryptedCheckpointStore.Save(path, context, [first], 1, key));
            Check("byte-cap rejection leaves no checkpoint", !File.Exists(path));
            EncryptedCheckpointStore.Save(path, context, [first], localByteCap, key);
            Check("round trip", EncryptedCheckpointStore.Load(path, context, key)?.SequenceEqual([first]) == true);
            var firstBytes = File.ReadAllBytes(path);
            Reject<InvalidDataException>("checkpoint replacement obeys temporary byte cap", () =>
                EncryptedCheckpointStore.Save(path, context, [first, second], firstBytes.Length, key));
            Check("capacity rejection preserves prior checkpoint", File.ReadAllBytes(path).SequenceEqual(firstBytes));
            EncryptedCheckpointStore.Save(path, context, [first, second], localByteCap, key);
            Check("append immutable page", EncryptedCheckpointStore.Load(path, context, key)?.SequenceEqual([first, second]) == true);
            if (OperatingSystem.IsWindows())
            {
                var users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
                var parent = new DirectoryInfo(directory);
                var parentAcl = FileSystemAclExtensions.GetAccessControl(parent);
                parentAcl.AddAccessRule(new FileSystemAccessRule(users, FileSystemRights.Read,
                    AccessControlType.Allow));
                FileSystemAclExtensions.SetAccessControl(parent, parentAcl);
                Reject<InvalidDataException>("broad directory denies checkpoint load", () =>
                    EncryptedCheckpointStore.Load(path, context, key));
                parentAcl = FileSystemAclExtensions.GetAccessControl(parent);
                parentAcl.PurgeAccessRules(users);
                FileSystemAclExtensions.SetAccessControl(parent, parentAcl);

                var checkpoint = new FileInfo(path);
                var fileAcl = FileSystemAclExtensions.GetAccessControl(checkpoint);
                fileAcl.AddAccessRule(new FileSystemAccessRule(users, FileSystemRights.Read,
                    AccessControlType.Allow));
                FileSystemAclExtensions.SetAccessControl(checkpoint, fileAcl);
                Reject<InvalidDataException>("broad file denies checkpoint load", () =>
                    EncryptedCheckpointStore.Load(path, context, key));
                fileAcl = FileSystemAclExtensions.GetAccessControl(checkpoint);
                fileAcl.PurgeAccessRules(users);
                FileSystemAclExtensions.SetAccessControl(checkpoint, fileAcl);
            }
            Reject<InvalidDataException>("no page removal", () => EncryptedCheckpointStore.Save(path, context, [first], localByteCap, key));
            Reject<InvalidDataException>("no page rewrite", () => EncryptedCheckpointStore.Save(path, context,
                [first with { ContentSha256 = new string('c', 64) }, second], localByteCap, key));
            Reject<InvalidDataException>("no completed row-count rewrite", () =>
                EncryptedCheckpointStore.Save(path, context, [first with { RowCount = 4 }, second], localByteCap, key));
            Reject<InvalidDataException>("no page after terminal", () => EncryptedCheckpointStore.Save(path,
                context, [first, second, first with { PageBoundary = "page-3" }], localByteCap, key));
            Reject<CryptographicException>("wrong key", () =>
                EncryptedCheckpointStore.Load(path, context, RandomNumberGenerator.GetBytes(32)));
            Reject<CryptographicException>("wrong context", () =>
                EncryptedCheckpointStore.Load(path, context with { PolicyVersion = "other" }, key));
            var bytes = File.ReadAllBytes(path);
            var oldHeader = (byte[])bytes.Clone();
            oldHeader[3] = (byte)'1';
            File.WriteAllBytes(path, oldHeader);
            Reject<InvalidDataException>("old count-less ledger version rejected", () =>
                EncryptedCheckpointStore.Load(path, context, key));
            File.WriteAllBytes(path, bytes);
            bytes[^1] ^= 1;
            File.WriteAllBytes(path, bytes);
            Reject<CryptographicException>("tampered ciphertext", () => EncryptedCheckpointStore.Load(path, context, key));
            Reject<CryptographicException>("corrupt prior file cannot be overwritten", () =>
                EncryptedCheckpointStore.Save(path, context, [first, second], localByteCap, key));
            File.Delete(path);
            Reject<InvalidDataException>("no duplicate page", () =>
                EncryptedCheckpointStore.Save(path, context, [first, first], localByteCap, key));
            Reject<ArgumentException>("key length", () =>
                EncryptedCheckpointStore.Save(path, context, [first], localByteCap, new byte[16]));
            Reject<InvalidDataException>("relative checkpoint path", () =>
                EncryptedCheckpointStore.Save("checkpoint.enc", context, [first], localByteCap, key));
            Check("rejected saves leave no file", !File.Exists(path));
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
                throw new Exception($"{name}: unexpected checkpoint result.");
            }

            count++;
        }

        void Reject<T>(string name, Action action) where T : Exception
        {
            try
            {
                action();
                throw new Exception($"{name}: checkpoint operation was accepted.");
            }
            catch (T)
            {
                count++;
            }
        }
    }
}
