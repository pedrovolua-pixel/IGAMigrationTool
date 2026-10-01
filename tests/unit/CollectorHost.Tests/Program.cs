using System.Text.Json;
using CollectorHost;

if (args is ["--hold-lease", var heldConfigPath, var heldScopeText] &&
    Guid.TryParse(heldScopeText, out var heldScope))
{
    using var heldLease = CrossProcessRunLease.TryAcquire(heldConfigPath, heldScope);
    if (heldLease is null)
    {
        Environment.ExitCode = 2;
        return;
    }

    Console.WriteLine("LEASE_HELD");
    await Task.Delay(Timeout.InfiniteTimeSpan);
    return;
}

var count = 0;
var configPath = @"C:\ProgramData\IgaPilotCollector\collector.json";
var outputPath = @"D:\Evidence\baseline.igapkg";
var valid = JsonSerializer.Serialize(new
{
    schemaVersion = 1,
    scopeId = "11111111-1111-1111-1111-111111111111",
    exactBuild = "10.0.0.1",
    queryPackId = "22222222-2222-2222-2222-222222222222",
    queryPackVersion = 1,
    queryPackSha256 = new string('a', 64),
    fieldPolicyId = "33333333-3333-3333-3333-333333333333",
    fieldPolicyVersion = 1,
    fieldPolicySha256 = new string('b', 64),
    sqlDescriptorRef = @"C:\ProgramData\IgaPilotCollector\sql.ref",
    timeZoneId = "America/New_York",
    localRunTime = "02:30",
    enabled = false,
    maxPageSize = 100,
    maxRows = 1000,
    maxDurationSeconds = 60,
    maxLocalBytes = 1000000,
    retentionHours = 24,
    offlineRecipientKeyId = "44444444-4444-4444-4444-444444444444"
});

Check("status", ["status", "--config", configPath], true);
Check("collect once", ["collect-once", "--config", configPath, "--offline-output", outputPath], true);
Check("service", ["service", "--config", configPath], true);
Check("no arbitrary argument", ["status", "--config", configPath, "--sql", "SELECT 1"], false);
Check("no UNC config", ["status", "--config", @"\\host\share\config.json"], false);
Check("no traversal config", ["status", "--config", @"C:\safe\..\config.json"], false);
Check("no device config", ["status", "--config", @"\\?\C:\config.json"], false);
Check("no reserved device name", ["status", "--config", @"C:\safe\CON.json"], false);
Check("no padded device name", ["status", "--config", @"C:\safe\CON .json"], false);
Check("no alternate stream", ["status", "--config", @"C:\safe\config.json:stream"], false);
Check("no duplicate path", ["collect-once", "--config", configPath, "--offline-output", configPath], false);
Check("no relative output", ["collect-once", "--config", configPath, "--offline-output", "out.pkg"], false);
Check("no output traversal", ["collect-once", "--config", configPath, "--offline-output", @"C:\safe\..\out.pkg"], false);

_ = CollectorConfig.Parse(valid);
count++;
Reject("unknown property", valid[..^1] + ",\"sqlText\":\"SELECT 1\"}");
Reject("duplicate property", valid[..^1] + ",\"schemaVersion\":1}");
Reject("future schema", valid.Replace("\"schemaVersion\":1", "\"schemaVersion\":2", StringComparison.Ordinal));
Reject("bad digest", valid.Replace(new string('a', 64), "bad", StringComparison.Ordinal));
Reject("network descriptor", valid.Replace(@"C:\\ProgramData\\IgaPilotCollector\\sql.ref", @"\\\\host\\share\\sql.ref", StringComparison.Ordinal));
Reject("retention past maximum", valid.Replace("\"retentionHours\":24", "\"retentionHours\":721", StringComparison.Ordinal));
Reject("negative page limit", valid.Replace("\"maxPageSize\":100", "\"maxPageSize\":-1", StringComparison.Ordinal));
Reject("bad time zone", valid.Replace("America/New_York", "No/Such_Zone", StringComparison.Ordinal));

var zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
var spring = DailySchedule.Next(new DateTimeOffset(2026, 3, 8, 0, 0, 0, TimeSpan.FromHours(-5)), zone, new TimeOnly(2, 30));
Expect("spring gap skipped", spring == new DateTimeOffset(2026, 3, 9, 2, 30, 0, TimeSpan.FromHours(-4)));
var fall = DailySchedule.Next(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.FromHours(-4)), zone, new TimeOnly(1, 30));
Expect("fall later occurrence", fall == new DateTimeOffset(2026, 11, 1, 1, 30, 0, TimeSpan.FromHours(-5)));
var missed = DailySchedule.Next(new DateTimeOffset(2026, 11, 1, 3, 0, 0, TimeSpan.FromHours(-5)), zone, new TimeOnly(1, 30));
Expect("missed occurrence not replayed", missed == new DateTimeOffset(2026, 11, 2, 1, 30, 0, TimeSpan.FromHours(-5)));

var leaseDirectory = Path.Combine(Path.GetTempPath(), "iga-lease-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(leaseDirectory);
try
{
    var leaseConfig = Path.Combine(leaseDirectory, "collector.json");
    var scope = Guid.NewGuid();
    using (var first = CrossProcessRunLease.TryAcquire(leaseConfig, scope))
    {
        Expect("first run lease", first is not null);
        using var second = CrossProcessRunLease.TryAcquire(leaseConfig, scope);
        Expect("overlap rejected", second is null);
        using var other = CrossProcessRunLease.TryAcquire(leaseConfig, Guid.NewGuid());
        Expect("different scope independent", other is not null);
    }

    using var resumed = CrossProcessRunLease.TryAcquire(leaseConfig, scope);
    Expect("released lease reacquired", resumed is not null);
}
finally
{
    Directory.Delete(leaseDirectory, true);
}

Console.WriteLine($"{count} collector host contract cases passed.");
Console.WriteLine($"{CheckpointStoreChecks.Run()} encrypted checkpoint-store cases passed.");
Console.WriteLine($"{await RunCoordinatorChecks.RunAsync(CollectorConfig.Parse(valid) with { Enabled = true })} shared run-coordinator cases passed.");
Console.WriteLine($"{await LeaseCrashRecoveryChecks.RunAsync()} cross-process lease recovery cases passed.");
if (OperatingSystem.IsWindows())
{
    Console.WriteLine($"{WindowsKeyStoreChecks.Run()} protected Windows key-store cases passed.");
}

void Check(string name, string[] args, bool expected)
{
    Expect(name, CollectorArguments.TryParse(args, out _) == expected);
}

void Reject(string name, string json)
{
    try
    {
        _ = CollectorConfig.Parse(json);
        throw new Exception($"{name}: config was accepted.");
    }
    catch (FormatException)
    {
        count++;
    }
}

void Expect(string name, bool condition)
{
    if (!condition)
    {
        throw new Exception($"{name}: unexpected result.");
    }

    count++;
}
