using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace CollectorHost;

public static class WindowsProtectedConfig
{
    [SupportedOSPlatform("windows")]
    public static CollectorConfig Read(string path)
    {
        var bytes = ReadProtectedBytes(path, 65536);
        try
        {
            return CollectorConfig.Parse(new UTF8Encoding(false, true).GetString(bytes));
        }
        catch (DecoderFallbackException error)
        {
            throw new FormatException("Invalid collector configuration encoding.", error);
        }
        finally
        {
            Array.Clear(bytes);
        }
    }

    [SupportedOSPlatform("windows")]
    public static byte[] ReadProtectedBytes(string path, int maxLength)
    {
        if (!LocalPath.IsValid(path) || maxLength <= 0)
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

        if (file.Length <= 0 || file.Length > maxLength)
        {
            throw new InvalidOperationException("CONFIG_INVALID");
        }

        var bytes = File.ReadAllBytes(path);
        if (bytes.Length == 0 || bytes.Length > maxLength)
        {
            Array.Clear(bytes);
            throw new InvalidOperationException("CONFIG_INVALID");
        }

        return bytes;
    }

    [SupportedOSPlatform("windows")]
    internal static void ValidateProtectedStageDirectory(string path)
    {
        if (!LocalPath.IsValid(path))
        {
            throw new InvalidOperationException("STAGE_PROTECTION_INVALID");
        }

        var directory = new DirectoryInfo(path);
        if (!directory.Exists)
        {
            throw new InvalidOperationException("STAGE_PROTECTION_INVALID");
        }

        var permitted = StagePrincipals();
        for (var current = directory; current is not null; current = current.Parent)
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException("STAGE_PROTECTION_INVALID");
            }

            var security = FileSystemAclExtensions.GetAccessControl(current);
            if (current.FullName == directory.FullName)
            {
                CheckStageAcl(security, permitted, true);
            }
            else
            {
                CheckAcl(security, permitted, false);
            }
        }
    }

    [SupportedOSPlatform("windows")]
    internal static void ValidateProtectedStageFile(string path)
    {
        if (!LocalPath.IsValid(path))
        {
            throw new InvalidOperationException("STAGE_PROTECTION_INVALID");
        }

        var file = new FileInfo(path);
        if (!file.Exists || (file.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("STAGE_PROTECTION_INVALID");
        }

        CheckStageAcl(FileSystemAclExtensions.GetAccessControl(file), StagePrincipals(), false);
    }

    [SupportedOSPlatform("windows")]
    private static HashSet<SecurityIdentifier> StagePrincipals()
    {
        var current = WindowsIdentity.GetCurrent().User ??
            throw new InvalidOperationException("STAGE_PROTECTION_INVALID");
        return
        [
            current,
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null)
        ];
    }

    [SupportedOSPlatform("windows")]
    private static void CheckStageAcl(FileSystemSecurity security, HashSet<SecurityIdentifier> permitted,
        bool requireProtectedRules)
    {
        if ((requireProtectedRules && !security.AreAccessRulesProtected) ||
            security.GetOwner(typeof(SecurityIdentifier)) is not SecurityIdentifier owner ||
            !permitted.Contains(owner))
        {
            throw new InvalidOperationException("STAGE_PROTECTION_INVALID");
        }

        foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
        {
            if (rule.AccessControlType == AccessControlType.Allow &&
                !permitted.Contains((SecurityIdentifier)rule.IdentityReference))
            {
                throw new InvalidOperationException("STAGE_PROTECTION_INVALID");
            }
        }
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
