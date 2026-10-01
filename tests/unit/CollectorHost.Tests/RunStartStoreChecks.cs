using System.Security.Cryptography;
using System.Text;
using CollectorHost;
using CollectorSafety;

internal static class RunStartStoreChecks
{
    public static int Run()
    {
        var count = 0;
        var directory = Path.Combine(Path.GetTempPath(), "iga-start-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        if (OperatingSystem.IsWindows()) WindowsStageTestDirectory.Protect(directory);
        try
        {
            var context = new PageCheckpointContext("inventory", "pack-1", "10.0.0.287", "scope-1",
                "policy-1", "uid");
            var key = RandomNumberGenerator.GetBytes(32);
            var path = Path.Combine(directory, "run-start.igr");
            var start = DateTimeOffset.UtcNow.AddHours(-2);

            Reject("future new start", () => EncryptedRunStartStore.LoadOrCreate(path, context,
                DateTimeOffset.UtcNow.AddHours(1), key));
            Check("future start made no record", !File.Exists(path));
            Check("new start persisted", EncryptedRunStartStore.LoadOrCreate(path, context, start, key) == start);
            var bytes = File.ReadAllBytes(path);
            Check("start is encrypted", !Encoding.UTF8.GetString(bytes).Contains("inventory",
                StringComparison.Ordinal));
            Check("existing start ignores later proposal", EncryptedRunStartStore.LoadOrCreate(path, context,
                DateTimeOffset.UtcNow.AddHours(1), key) == start);
            Check("existing start loads exactly", EncryptedRunStartStore.Load(path, context, key) == start);
            Reject("wrong key", () => EncryptedRunStartStore.Load(path, context,
                RandomNumberGenerator.GetBytes(32)));
            Reject("wrong context", () => EncryptedRunStartStore.Load(path,
                context with { ScopeId = "another-scope" }, key));

            var changed = (byte[])bytes.Clone();
            changed[^1] ^= 1;
            File.WriteAllBytes(path, changed);
            Reject("tampered ciphertext", () => EncryptedRunStartStore.Load(path, context, key));
            File.WriteAllBytes(path, bytes);
            changed = (byte[])bytes.Clone();
            changed[3] = (byte)'0';
            File.WriteAllBytes(path, changed);
            Reject("old header", () => EncryptedRunStartStore.Load(path, context, key));
            File.Delete(path);

            File.WriteAllText(Path.Combine(directory, "orphan.stage"), "orphan");
            Reject("stage without start cannot reset clock", () => EncryptedRunStartStore.LoadOrCreate(path,
                context, DateTimeOffset.UtcNow, key));
            Check("orphan stage made no start", !File.Exists(path));
            return count;
        }
        finally
        {
            Directory.Delete(directory, true);
        }

        void Check(string name, bool okay)
        {
            if (!okay) throw new Exception($"{name}: unexpected result.");
            count++;
        }

        void Reject(string name, Action action)
        {
            try
            {
                action();
                throw new Exception($"{name}: accepted invalid record.");
            }
            catch (Exception error) when (error is InvalidDataException or CryptographicException)
            {
                count++;
            }
        }
    }
}
