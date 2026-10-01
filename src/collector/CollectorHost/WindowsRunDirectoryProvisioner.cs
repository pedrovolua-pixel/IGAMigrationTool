using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace CollectorHost;

// Isolated local primitive. A reviewed installer/adapter must choose the parent,
// bind run IDs to extraction records, and own cleanup before customer use.
internal static class WindowsRunDirectoryProvisioner
{
    [SupportedOSPlatform("windows")]
    internal static string ProvisionNew(string protectedParent, Guid runId)
    {
        if (runId == Guid.Empty || !LocalPath.IsValid(protectedParent))
        {
            throw new InvalidDataException("Run directory input is invalid.");
        }

        WindowsProtectedConfig.ValidateProtectedStageDirectory(protectedParent);
        var path = Path.Combine(protectedParent, runId.ToString("N"));
        if (Directory.Exists(path) || File.Exists(path))
        {
            throw new IOException("Run directory already exists.");
        }

        var current = WindowsIdentity.GetCurrent().User ??
            throw new InvalidOperationException("Run identity is unavailable.");
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(true, false);
        security.SetOwner(current);
        foreach (var principal in new[]
                 {
                     current,
                     new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                     new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null)
                 })
        {
            security.AddAccessRule(new FileSystemAccessRule(principal, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None, AccessControlType.Allow));
        }

        security.CreateDirectory(path);
        WindowsProtectedConfig.ValidateProtectedStageDirectory(path);
        return path;
    }
}
