using System.Diagnostics;

var directory = new DirectoryInfo(AppContext.BaseDirectory);
while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "tests/unit/ArtifactReviewComponent.Tests/verify.mjs")))
{
    directory = directory.Parent;
}
if (directory is null)
{
    Console.Error.WriteLine("B13 component runner cannot locate the actual verifier source.");
    return 1;
}
var runtime = Environment.GetEnvironmentVariable("IGA_NODE_EXECUTABLE") ?? "node";
using var process = new Process
{
    StartInfo = new ProcessStartInfo(runtime)
    {
        WorkingDirectory = directory.FullName,
        UseShellExecute = false,
    },
};
process.StartInfo.ArgumentList.Add("tests/unit/ArtifactReviewComponent.Tests/verify.mjs");
try
{
    if (!process.Start())
    {
        Console.Error.WriteLine("B13 Node component verification did not start.");
        return 1;
    }
    await process.WaitForExitAsync();
    return process.ExitCode;
}
catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
{
    Console.Error.WriteLine($"B13 Node component verification could not execute: {exception.GetType().Name}");
    return 1;
}
