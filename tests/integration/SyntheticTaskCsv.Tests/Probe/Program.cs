using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using SyntheticTaskCsv;

var mode = Path.GetFileName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
var input = await Console.In.ReadToEndAsync();
var envelope = CsvCodec.Parse(input).Envelope!;
if (mode == "probe-timeout") Thread.Sleep(30_000);
if (mode == "probe-output") { Console.Write(new string('x', 5 * 1024 * 1024)); return 0; }
if (mode == "probe-stderr") { Console.Error.Write(new string('x', 32 * 1024)); return 0; }
if (mode == "probe-rss") { while (true) { var pointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(16 * 1024 * 1024); for (var i = 0; i < 16 * 1024 * 1024; i += 4096) System.Runtime.InteropServices.Marshal.WriteByte(pointer, i, 1); } }
if (mode == "probe-memory") { try { var retained = new List<byte[]>(); while (true) retained.Add(new byte[32 * 1024 * 1024]); } catch (OutOfMemoryException) { return 2; } }
if (mode == "probe-env" && Environment.GetEnvironmentVariables().Keys.Cast<string>().Any(key => key.Contains("SECRET") || key.Contains("PASSWORD") || key.Contains("CONNECTION") || key == "PATH")) return 3;
try
{
    if (mode == "probe-network") { using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp); socket.SendTo([1], new IPEndPoint(IPAddress.Loopback, 9)); return 3; }
    if (mode == "probe-write") { File.WriteAllText("/private/tmp/iga-csv-probe-forbidden-output", "forbidden"); return 3; }
    if (mode == "probe-read") { File.ReadAllText("/private/tmp/iga-csv-credential-marker"); return 3; }
    if (mode == "probe-fork") { using var process = Process.Start(new ProcessStartInfo("/bin/sh", "-c true") { UseShellExecute = false }); process!.WaitForExit(); return 3; }
}
catch (Exception exception) when (exception is UnauthorizedAccessException or SocketException or IOException or System.ComponentModel.Win32Exception) { }
using var output = Console.OpenStandardOutput();
await output.WriteAsync(CsvCodec.Render(envelope));
return 0;
