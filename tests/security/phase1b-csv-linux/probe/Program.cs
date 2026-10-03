using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using SyntheticTaskCsv;

if (args.Length != 1) return 3;
using var input = Console.OpenStandardInput();
using var buffer = new MemoryStream();
input.CopyTo(buffer);
if (buffer.Length > CsvCodec.MaximumInputBytes) return 3;
var parsed = CsvCodec.Parse(new System.Text.UTF8Encoding(false, true).GetString(buffer.ToArray()));
if (!parsed.Succeeded) return 3;
try
{
    switch (args[0])
    {
        case "benign": break;
        case "identity":
            var uid = File.ReadLines("/proc/self/status").Single(line => line.StartsWith("Uid:", StringComparison.Ordinal)).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (uid[2] != "10001") return 3;
            break;
        case "environment":
            string[] allowed = ["DOTNET_EnableDiagnostics", "DOTNET_GCHeapHardLimit", "DOTNET_SYSTEM_GLOBALIZATION_INVARIANT", "HOME", "TMPDIR"];
            if (Environment.GetEnvironmentVariables().Keys.Cast<string>().Any(key => !allowed.Contains(key, StringComparer.Ordinal))) return 3;
            break;
        case "network":
            try { using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp); socket.SendTo(new byte[] { 0 }, new IPEndPoint(IPAddress.Loopback, 9)); return 3; }
            catch (SocketException exception) when (exception.SocketErrorCode == SocketError.AccessDenied) { }
            break;
        case "write-app":
        case "write-temp":
            try { File.WriteAllText(args[0] == "write-app" ? "/app/forbidden" : "/tmp/forbidden", "synthetic"); return 3; }
            catch (UnauthorizedAccessException) { }
            catch (IOException exception) when ((exception.HResult & 0xffff) == 30) { }
            break;
        case "untrusted-read":
            try { File.ReadAllText("/root/phase1b-untrusted-marker"); return 3; }
            catch (UnauthorizedAccessException) { }
            break;
        case "fork":
            try { using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("/bin/sh", "-c true") { UseShellExecute = false }); process?.WaitForExit(); return 3; }
            catch (Win32Exception exception) when (exception.NativeErrorCode == 1) { }
            break;
        case "wall": Thread.Sleep(TimeSpan.FromSeconds(30)); return 3;
        case "stdout": Console.OpenStandardOutput().Write(new byte[5 * 1024 * 1024]); return 3;
        case "stderr": Console.OpenStandardError().Write(new byte[32 * 1024]); return 3;
        case "cpu": while (true) { Thread.SpinWait(10000); }
        case "managed-memory":
            var managed = new List<byte[]>();
            try { while (true) { managed.Add(new byte[16 * 1024 * 1024]); } }
            catch (OutOfMemoryException) { return 2; }
        case "native-memory":
            while (true) { var address = Marshal.AllocHGlobal(16 * 1024 * 1024); for (var offset = 0; offset < 16 * 1024 * 1024; offset += 4096) Marshal.WriteByte(address, offset, 1); }
        case "pids":
            try { while (true) { new Thread(() => Thread.Sleep(Timeout.Infinite)) { IsBackground = true }.Start(); } }
            catch (OutOfMemoryException) { return 2; }
        default: return 3;
    }
    Console.OpenStandardOutput().Write(CsvCodec.Render(parsed.Envelope!));
    return 0;
}
catch (Exception exception) when (exception is not OutOfMemoryException) { return 3; }
