using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

internal static class WindowsStageTestDirectory
{
    [SupportedOSPlatform("windows")]
    internal static void Protect(string directory)
    {
        var current = WindowsIdentity.GetCurrent().User ?? throw new Exception("Missing Windows identity.");
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

        FileSystemAclExtensions.SetAccessControl(new DirectoryInfo(directory), security);
    }
}
