namespace CollectorHost;

// Called while the extraction holds its run lease. All files in one protected
// run directory count against the local byte cap, including metadata.
internal static class LocalRunDirectoryCapacity
{
    internal static void Validate(string directory, long maxLocalBytes, long incomingBytes)
    {
        if (maxLocalBytes <= 0 || incomingBytes < 0)
        {
            throw new InvalidDataException("Local staging byte limit is invalid.");
        }

        var remaining = maxLocalBytes;
        foreach (var path in Directory.EnumerateFileSystemEntries(directory))
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
            {
                throw new InvalidDataException("Staging directory contains an unsupported entry.");
            }

            if (OperatingSystem.IsWindows())
            {
                try
                {
                    WindowsProtectedConfig.ValidateProtectedStageFile(path);
                }
                catch (InvalidOperationException error)
                {
                    throw new InvalidDataException("Run file protection is invalid.", error);
                }
            }

            var length = new FileInfo(path).Length;
            if (length < 0 || length > remaining)
            {
                throw new InvalidDataException("Local staging byte limit is reached.");
            }

            remaining -= length;
        }

        if (incomingBytes > remaining)
        {
            throw new InvalidDataException("Local staging byte limit is reached.");
        }
    }
}
