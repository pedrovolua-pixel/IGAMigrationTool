using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using CollectorHost;
using CollectorSafety;

internal static class PageStageStoreChecks
{
    public static int Run()
    {
        var count = 0;
        var directory = Path.Combine(Path.GetTempPath(), "iga-stage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        if (OperatingSystem.IsWindows()) WindowsStageTestDirectory.Protect(directory);
        try
        {
            var context = new PageCheckpointContext("inventory", "pack-1", "10.0.0.287", "scope-1",
                "policy-1", "uid");
            var key = RandomNumberGenerator.GetBytes(32);
            const string boundary = "opaque-boundary";
            var fields = new MinimizedField[]
            {
                new(new FieldKey("schema", "uid"), FieldDisposition.Included, "permitted-marker"),
                new(new FieldKey("schema", "display"), FieldDisposition.Excluded, null)
            };
            var digest = MinimizedPageDigest.Compute(boundary, 1, fields);
            Check("first page staged", EncryptedPageStageStore.Stage(directory, context, boundary, 1,
                digest, fields, key) == EncryptedPageStageStore.StageResult.NewPage);
            var files = Directory.GetFiles(directory, "*.stage");
            Check("one opaque staged file", files.Length == 1 && !files[0].Contains(boundary,
                StringComparison.Ordinal));
            var bytes = File.ReadAllBytes(files[0]);
            Check("no plaintext value or boundary", !Encoding.UTF8.GetString(bytes).Contains("permitted-marker",
                StringComparison.Ordinal) && !Encoding.UTF8.GetString(bytes).Contains(boundary,
                StringComparison.Ordinal));
            var recovered = EncryptedPageStageStore.Load(directory, context, boundary, key);
            Check("new instance recovers minimized page", recovered?.RowCount == 1 &&
                recovered.Digest == digest && recovered.Fields[0].IncludedValue == "permitted-marker" &&
                recovered.Fields[1].IncludedValue is null);
            Check("same page replay is idempotent", EncryptedPageStageStore.Stage(directory, context,
                boundary, 1, digest, fields, key) == EncryptedPageStageStore.StageResult.IdempotentReplay &&
                Directory.GetFiles(directory, "*.stage").Length == 1);

            var changed = new MinimizedField[] { fields[0] with { IncludedValue = "changed" }, fields[1] };
            Reject("changed content conflicts", () => EncryptedPageStageStore.Stage(directory, context,
                boundary, 1, MinimizedPageDigest.Compute(boundary, 1, changed), changed, key));
            Reject("digest mismatch rejected", () => EncryptedPageStageStore.Stage(directory, context,
                "new-page", 1, digest, fields, key));
            Reject("prohibited field rejected", () => EncryptedPageStageStore.Stage(directory, context,
                "prohibited", 1, MinimizedPageDigest.Compute("prohibited", 1,
                    [new MinimizedField(fields[0].Key, FieldDisposition.Prohibited, null)]),
                [new MinimizedField(fields[0].Key, FieldDisposition.Prohibited, null)], key));
            Reject("excluded value rejected", () => EncryptedPageStageStore.Stage(directory, context,
                "excluded", 1, MinimizedPageDigest.Compute("excluded", 1,
                    [fields[1] with { IncludedValue = "leak" }]),
                [fields[1] with { IncludedValue = "leak" }], key));
            var large = new MinimizedField(fields[0].Key, FieldDisposition.Included,
                new string('x', 1024 * 1024));
            Reject("oversized page rejected before file creation", () => EncryptedPageStageStore.Stage(
                directory, context, "large", 1, MinimizedPageDigest.Compute("large", 1, [large]),
                [large], key));
            Check("wrong key cannot locate prior page", EncryptedPageStageStore.Load(directory, context,
                boundary, RandomNumberGenerator.GetBytes(32)) is null);
            Check("other context has no matching page", EncryptedPageStageStore.Load(directory,
                context with { ScopeId = "other" }, boundary, key) is null);

            if (OperatingSystem.IsWindows())
            {
                var security = FileSystemAclExtensions.GetAccessControl(new DirectoryInfo(directory));
                security.AddAccessRule(new FileSystemAccessRule(
                    new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
                    FileSystemRights.Read, AccessControlType.Allow));
                FileSystemAclExtensions.SetAccessControl(new DirectoryInfo(directory), security);
                Reject("broad stage directory rejected", () =>
                    EncryptedPageStageStore.Load(directory, context, boundary, key));
                WindowsStageTestDirectory.Protect(directory);

                var stagedFile = new FileInfo(files[0]);
                var fileSecurity = FileSystemAclExtensions.GetAccessControl(stagedFile);
                fileSecurity.AddAccessRule(new FileSystemAccessRule(
                    new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
                    FileSystemRights.Read, AccessControlType.Allow));
                FileSystemAclExtensions.SetAccessControl(stagedFile, fileSecurity);
                Reject("broad stage file rejected", () =>
                    EncryptedPageStageStore.Load(directory, context, boundary, key));
                fileSecurity = FileSystemAclExtensions.GetAccessControl(stagedFile);
                fileSecurity.RemoveAccessRuleAll(new FileSystemAccessRule(
                    new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
                    FileSystemRights.Read, AccessControlType.Allow));
                FileSystemAclExtensions.SetAccessControl(stagedFile, fileSecurity);
            }

            bytes[bytes.Length - 1] ^= 1;
            File.WriteAllBytes(files[0], bytes);
            RejectCrypto("tampering rejected", () => EncryptedPageStageStore.Load(directory, context,
                boundary, key));
            Check("no plaintext temporary files", Directory.GetFiles(directory, "*.tmp").Length == 0);
            return count;
        }
        finally
        {
            Directory.Delete(directory, true);
        }

        void Check(string name, bool okay)
        {
            if (!okay) throw new Exception($"{name}: unexpected stage-store result.");
            count++;
        }

        void Reject(string name, Action action)
        {
            try { action(); throw new Exception($"{name}: accepted invalid staged page."); }
            catch (InvalidDataException) { count++; }
        }

        void RejectCrypto(string name, Action action)
        {
            try { action(); throw new Exception($"{name}: accepted invalid staged page."); }
            catch (CryptographicException) { count++; }
        }

    }
}
