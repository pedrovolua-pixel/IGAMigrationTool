using System.Diagnostics;
using System.Reflection;
using CollectorHost;

internal static class LeaseCrashRecoveryChecks
{
    public static async Task<int> RunAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), "iga-lease-crash-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var configPath = Path.Combine(directory, "collector.json");
            var scope = Guid.NewGuid();
            var start = new ProcessStartInfo("dotnet")
            {
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            start.ArgumentList.Add("--hold-lease");
            start.ArgumentList.Add(configPath);
            start.ArgumentList.Add(scope.ToString("D"));
            using var child = Process.Start(start) ?? throw new Exception("Lease child did not start.");
            try
            {
                using var readyTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var ready = await child.StandardOutput.ReadLineAsync(readyTimeout.Token);
                if (ready != "LEASE_HELD")
                {
                    throw new Exception("Lease child did not acquire its lock.");
                }

                using var overlapping = CrossProcessRunLease.TryAcquire(configPath, scope);
                if (overlapping is not null)
                {
                    throw new Exception("Another process acquired the held lease.");
                }
            }
            finally
            {
                if (!child.HasExited)
                {
                    child.Kill(entireProcessTree: true);
                }

                await child.WaitForExitAsync();
            }

            using var recovered = CrossProcessRunLease.TryAcquire(configPath, scope);
            if (recovered is null)
            {
                throw new Exception("Lease could not be reacquired after child termination.");
            }

            return 2;
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
