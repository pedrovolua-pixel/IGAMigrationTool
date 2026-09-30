using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace CollectorHost;

public static class WindowsProtectedConfig
{
    [SupportedOSPlatform("windows")]
    public static CollectorConfig Read(string path)
    {
        if (!LocalPath.IsValid(path))
        {
            throw new InvalidOperationException("CONFIG_INVALID");
        }

        var file = new FileInfo(path);
        if (!file.Exists || (file.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("CONFIG_PROTECTION_INVALID");
        }

        var permitted = new HashSet<SecurityIdentifier>
        {
            new(WellKnownSidType.BuiltinAdministratorsSid, null),
            new(WellKnownSidType.LocalSystemSid, null)
        };
        var current = WindowsIdentity.GetCurrent().User;
        if (current is null)
        {
            throw new InvalidOperationException("CONFIG_PROTECTION_INVALID");
        }

        permitted.Add(current);
        CheckAcl(FileSystemAclExtensions.GetAccessControl(file), permitted, true);
        var immediateParent = file.Directory ?? throw new InvalidOperationException("CONFIG_PROTECTION_INVALID");
        CheckAcl(FileSystemAclExtensions.GetAccessControl(immediateParent), permitted, true);
        for (var directory = file.Directory; directory is not null; directory = directory.Parent)
        {
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException("CONFIG_PROTECTION_INVALID");
            }

            if (directory != immediateParent)
            {
                CheckAcl(FileSystemAclExtensions.GetAccessControl(directory), permitted, false);
            }
        }

        if (file.Length is <= 0 or > 65536)
        {
            throw new InvalidOperationException("CONFIG_INVALID");
        }

        return CollectorConfig.Parse(File.ReadAllText(path));
    }

    [SupportedOSPlatform("windows")]
    private static void CheckAcl(FileSystemSecurity security, HashSet<SecurityIdentifier> permitted,
        bool protectedEntry)
    {
        var mutating = protectedEntry
            ? FileSystemRights.Write | FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles |
              FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership
            : FileSystemRights.DeleteSubdirectoriesAndFiles | FileSystemRights.ChangePermissions |
              FileSystemRights.TakeOwnership;
        if (protectedEntry &&
            (security.GetOwner(typeof(SecurityIdentifier)) is not SecurityIdentifier owner || !permitted.Contains(owner)))
        {
            throw new InvalidOperationException("CONFIG_PROTECTION_INVALID");
        }

        foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
        {
            if (rule.AccessControlType == AccessControlType.Allow &&
                (rule.FileSystemRights & mutating) != 0 &&
                !permitted.Contains((SecurityIdentifier)rule.IdentityReference))
            {
                throw new InvalidOperationException("CONFIG_PROTECTION_INVALID");
            }
        }
    }
}
