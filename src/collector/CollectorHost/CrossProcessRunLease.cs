namespace CollectorHost;

public sealed class CrossProcessRunLease : IDisposable
{
    private readonly FileStream stream;

    private CrossProcessRunLease(FileStream stream)
    {
        this.stream = stream;
    }

    public static CrossProcessRunLease? TryAcquire(string protectedConfigPath, Guid scopeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedConfigPath);
        if (scopeId == Guid.Empty)
        {
            throw new ArgumentException("Scope must be nonempty.", nameof(scopeId));
        }

        var directory = Path.GetDirectoryName(protectedConfigPath) ??
            throw new ArgumentException("Config path has no directory.", nameof(protectedConfigPath));
        var path = Path.Combine(directory, $".iga-run-{scopeId:N}.lock");
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("Run lease path is a reparse point.");
        }

        try
        {
            return new CrossProcessRunLease(new FileStream(path, FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None, 1, FileOptions.None));
        }
        catch (IOException)
        {
            return null;
        }
    }

    public void Dispose() => stream.Dispose();
}
