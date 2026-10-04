using System.Text.Json;
using System.Text.Json.Serialization;

internal static class V14Program
{
    internal static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length == 2 && args[0] == "--prepare-browser-fixture")
            {
                var output = Path.GetFullPath(args[1]);
                if (!Path.IsPathFullyQualified(args[1]) || Path.GetFileName(output) != "historical-artifact-run.json" || Path.GetFileName(Path.GetDirectoryName(output)) != ".host") throw new InvalidOperationException("owned_browser_fixture_output");
                OwnedDatabase.Configure(OwnedDatabase.BrowserName);
                await SavedSourceCases.Engine().InitializeAsync();
                var run = await SavedSourceCases.Complete("synthetic-analysis-findings-v1", "synthetic-review-maturity-fix-review-equal-v1");
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                await File.WriteAllTextAsync(output, JsonSerializer.Serialize(DemoProjection.Detail(run), Web));
                Console.WriteLine($"PASS V14 actual owned historical browser fixture; checks={Check.Count}; no task activation or HTTP guard widening");
                return 0;
            }
            if (args.Length > 1 || args.Length == 1 && args[0] != "--portable") throw new InvalidOperationException("invalid_cli");
            OracleCases.Run();
            PortableCases.Run();
            Policies.Run();
            if (args.Length == 0) await DatabaseCases.Run();
            else Console.WriteLine("NOT VERIFIED TC14-T04/T05/T06/T12 PG: --portable excludes durable task-store execution.");
            Console.WriteLine($"PASS V14 independent planning tasks; checks={Check.Count}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"FAIL V14; code={Check.Last}; checks={Check.Count}; exception={exception.GetType().Name}; payload-suppressed");
            return 1;
        }
    }
}
