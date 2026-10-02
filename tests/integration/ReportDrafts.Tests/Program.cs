using System.Text.Json;
using ReportDrafts;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        if (args.Contains("--export-owned-fixture", StringComparer.Ordinal))
        {
            await File.WriteAllTextAsync("/private/tmp/iga-v6-owned-fixture.json", CoreFixture.Json(CoreFixture.Create()).GetRawText());
            return;
        }
        CoreCases.Run();
        await PersistenceCases.Run(args);
        Console.WriteLine($"{Check.Count} independent draft assertions passed. Synthetic read-only values only; publication and manual Windows acceptance NOT VERIFIED.");
    }
}
