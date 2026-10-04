using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using CollectorHost;

internal static class WindowsRunDirectoryChecks
{
    [SupportedOSPlatform("windows")]
    internal static int Run()
    {
        var checks = 0;
        var parent = Path.Combine(Path.GetTempPath(), "iga-runs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent);
        try
        {
            WindowsStageTestDirectory.Protect(parent);
            var runId = Guid.NewGuid();
            var path = WindowsRunDirectoryProvisioner.ProvisionNew(parent, runId);
            Check("protected empty run directory created", path == Path.Combine(parent, runId.ToString("N")) &&
                Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any());
            WindowsProtectedConfig.ValidateProtectedStageDirectory(path);
            Check("run directory passes protection checks", true);
            Reject<IOException>("existing run directory is not reused", () =>
                WindowsRunDirectoryProvisioner.ProvisionNew(parent, runId));
            Reject<InvalidDataException>("empty run ID rejected", () =>
                WindowsRunDirectoryProvisioner.ProvisionNew(parent, Guid.Empty));

            var broad = FileSystemAclExtensions.GetAccessControl(new DirectoryInfo(parent));
            broad.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
                FileSystemRights.Read, AccessControlType.Allow));
            FileSystemAclExtensions.SetAccessControl(new DirectoryInfo(parent), broad);
            Reject<InvalidOperationException>("broad parent cannot provision", () =>
                WindowsRunDirectoryProvisioner.ProvisionNew(parent, Guid.NewGuid()));
            Check("broad parent refusal creates no extra directory",
                Directory.GetDirectories(parent).Length == 1);
            return checks;
        }
        finally
        {
            Directory.Delete(parent, true);
        }

        void Check(string name, bool okay)
        {
            if (!okay) throw new Exception($"{name}: unexpected run-directory result.");
            checks++;
        }

        void Reject<T>(string name, Action action) where T : Exception
        {
            try
            {
                action();
                throw new Exception($"{name}: invalid run directory was accepted.");
            }
            catch (T)
            {
                checks++;
            }
        }
    }
}
