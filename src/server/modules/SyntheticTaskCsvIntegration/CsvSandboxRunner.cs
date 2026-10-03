using System.Diagnostics;
using System.Text;
using SyntheticTaskCsv;

namespace SyntheticTaskCsvIntegration;

public sealed record CsvSandboxRenderResult(CsvIssue? Issue, byte[]? Bytes) { public bool Succeeded => Issue is null && Bytes is not null; }
/// <summary>Declared local macOS runner. No environment inheritance, database/configuration input or arbitrary executable.</summary>
public sealed class CsvSandboxRunner
{
    public const string Dotnet = "/private/tmp/iga-dotnet-10.0.401/dotnet";
    public const int MaximumSeconds = 10, MaximumRssBytes = 384 * 1024 * 1024, MaximumStderrBytes = 16 * 1024;
    private readonly string rendererDirectory;
    public CsvSandboxRunner(string rendererDirectory)
    {
        this.rendererDirectory = Path.GetFullPath(rendererDirectory);
        if (!Path.IsPathFullyQualified(rendererDirectory) || !File.Exists(Path.Combine(this.rendererDirectory, "SyntheticCsvRenderer.dll"))) throw new ArgumentException("Declared renderer closure required.");
    }
    public static string CreateMacProfile(string rendererDirectory)
    {
        static string Literal(string path) => "(literal \"" + path.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\")";
        var directory = Path.GetFullPath(rendererDirectory);
        if (directory.Any(char.IsControl)) throw new ArgumentException("Invalid renderer path.");
        var files = new[] { "SyntheticCsvRenderer.dll", "SyntheticCsvRenderer.deps.json", "SyntheticCsvRenderer.runtimeconfig.json", "SyntheticTaskCsv.dll" };
        return "(version 1)(deny default)(allow process-exec " + Literal(Dotnet) + ")(deny process-fork)(deny network*)(deny file-write*)" +
            "(allow sysctl-read)(allow file-read-metadata)(allow file-read-data (subpath \"/private/tmp/iga-dotnet-10.0.401\") (subpath \"/usr/lib\") (subpath \"/System/Library\") " +
            Literal("/") + " " + Literal("/dev/null") + " " + Literal("/dev/urandom") + " " + string.Join(' ', files.Select(name => Literal(Path.Combine(directory, name)))) + ")";
    }
    public async Task<CsvSandboxRenderResult> RenderAsync(CsvEnvelope envelope, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsMacOS() || Environment.UserName == "root" || !File.Exists("/usr/bin/sandbox-exec") || !File.Exists(Dotnet)) return new(CsvIssue.RendererFailure, null);
        byte[] input;
        try { input = CsvCanonical.Bytes(envelope); if (!CsvCodec.Parse(new UTF8Encoding(false, true).GetString(input)).Succeeded) return new(CsvIssue.InvalidInput, null); }
        catch (Exception exception) when (exception is ArgumentException or System.Text.Json.JsonException) { return new(CsvIssue.InvalidInput, null); }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(MaximumSeconds));
        var start = new ProcessStartInfo("/usr/bin/sandbox-exec") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, WorkingDirectory = rendererDirectory };
        start.ArgumentList.Add("-p"); start.ArgumentList.Add(CreateMacProfile(rendererDirectory)); start.ArgumentList.Add(Dotnet); start.ArgumentList.Add(Path.Combine(rendererDirectory, "SyntheticCsvRenderer.dll"));
        start.Environment.Clear();
        start.Environment["DOTNET_EnableDiagnostics"] = "0";
        start.Environment["DOTNET_GCHeapHardLimit"] = "10000000";
        start.Environment["DOTNET_SYSTEM_GLOBALIZATION_INVARIANT"] = "1";
        start.Environment["HOME"] = "/nonexistent";
        start.Environment["TMPDIR"] = "/nonexistent";
        using var process = new Process { StartInfo = start };
        var started = false;
        try
        {
            if (!process.Start()) return new(CsvIssue.RendererFailure, null);
            started = true;
            var outputTask = ReadCapped(process.StandardOutput.BaseStream, CsvCodec.MaximumOutputBytes, deadline);
            var stderrTask = ReadCapped(process.StandardError.BaseStream, MaximumStderrBytes, deadline);
            var monitorTask = Monitor(process, deadline);
            await process.StandardInput.BaseStream.WriteAsync(input, deadline.Token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(deadline.Token);
            await monitorTask;
            var output = await outputTask; var stderr = await stderrTask;
            return process.ExitCode == 0 && stderr.Length == 0 && CsvCodec.Verify(envelope, output, Guid.NewGuid()).Succeeded ? new(null, output) : new(CsvIssue.RendererFailure, null);
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        { return new(CsvIssue.RendererFailure, null); }
        finally
        {
            if (started && !process.HasExited) { process.Kill(entireProcessTree: true); using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(1)); try { await process.WaitForExitAsync(stop.Token); } catch (OperationCanceledException) { } }
        }
    }
    private static async Task<byte[]> ReadCapped(Stream stream, int cap, CancellationTokenSource deadline)
    {
        using var result = new MemoryStream(); var buffer = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, deadline.Token); if (read == 0) return result.ToArray();
            if (result.Length + read > cap) { deadline.Cancel(); throw new IOException("Declared renderer stream limit."); }
            result.Write(buffer, 0, read);
        }
    }
    private static async Task Monitor(Process process, CancellationTokenSource deadline)
    {
        while (!process.HasExited)
        {
            process.Refresh();
            if (process.WorkingSet64 > MaximumRssBytes) { deadline.Cancel(); return; }
            try { await Task.Delay(25, deadline.Token); } catch (OperationCanceledException) { return; }
        }
    }
}
