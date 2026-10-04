using System.Security.Cryptography;
using System.Text.Json;

var directory = Path.Combine(AppContext.BaseDirectory, "fixtures");
using var cases = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "persisted-cases.json")));
using var sql = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "sql-surface.json")));
var expectedCases = cases.RootElement.GetProperty("cases").EnumerateArray().Select(x => x.GetProperty("id").GetString()).ToArray();
if (!expectedCases.SequenceEqual(Enumerable.Range(1, 18).Select(x => $"NPV-I{x:00}"))) throw new InvalidOperationException("Independent case oracle changed.");
if (sql.RootElement.GetProperty("functions").GetArrayLength() != 35 || sql.RootElement.GetProperty("compositeReturns").GetArrayLength() != 15)
    throw new InvalidOperationException("Independent SQL inventory changed.");
using var goldens = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "native-fixtures", "goldens.json")));
var count = 0;
foreach (var item in goldens.RootElement.GetProperty("files").EnumerateObject())
{
    var original = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "native-fixtures", item.Name));
    if (original.LongLength != item.Value.GetProperty("byteLength").GetInt64()
        || Convert.ToHexStringLower(SHA256.HashData(original)) != item.Value.GetProperty("sha256").GetString())
        throw new InvalidOperationException("Original independent native vector changed.");
    count++;
}
if (count != 31) throw new InvalidOperationException("Original vector inventory changed.");
Console.WriteLine("PASS: frozen 18 planned cases, 35 SQL signatures, 15 return shapes and unchanged 31 original byte commitments.");
_ = PreparationChecks.Run();
_ = await MarkerClassificationChecks.RunAsync();
Console.WriteLine("Persisted mechanisms NOT EXECUTED: standalone verifier preparation only.");

if (args.SequenceEqual(new[] { "--portable-adapter" }))
{
    var results = await AdapterPortableChecks.RunAsync();
    await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "portable-adapter-results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    if (results.Any(x => !x.Passed)) Environment.ExitCode = 1;
}
else if (args.SequenceEqual(new[] { "--audit-encoder" }))
{
    var results = AuditEncoderChecks.Run();
    await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "audit-encoder-results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    if (results.Any(x => !x.Passed)) Environment.ExitCode = 1;
}
else if (args.SequenceEqual(new[] { "--protocol-cleanup" }))
{
    var observation = await ProtocolCleanupChecks.RunAsync();
    await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "protocol-cleanup-results.json"), JsonSerializer.Serialize(observation, new JsonSerializerOptions { WriteIndented = true }));
}
else if (args.SequenceEqual(new[] { "--core-cleanup" }))
{
    var results = await MarkerClassificationChecks.RunCleanupAsync();
    await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "core-cleanup-results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    if (results.Any(x => !x.Passed)) Environment.ExitCode = 1;
}
else if (args.SequenceEqual(new[] { "--portable-observer" }))
{
    var results = await CancellationObserverChecks.RunAsync();
    await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "portable-observer-results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    if (results.Any(x => !x.Passed)) Environment.ExitCode = 1;
}
else if (args.SequenceEqual(new[] { "--portable-wire" }))
{
    var results = AdapterWireChecks.Run();
    await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "portable-wire-results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    if (results.Any(x => !x.Passed)) Environment.ExitCode = 1;
}
else if (args.SequenceEqual(new[] { "--verify-preconditions" }))
{
    using var bounded = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    await OwnedProvisioning.CheckFreshAbsenceAsync(bounded.Token);
}
else if (args.Length != 0) throw new InvalidOperationException("Unknown independent verifier mode.");
