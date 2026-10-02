internal static class Program
{
    private static async Task Main(string[] args)
    {
        MaturityCases.Run();
        ReviewPolicyCases.Run();
        await ReviewStoreCases.Run(args);
        await BridgeCases.Run(args);
        Console.WriteLine($"{Check.Count} independent review/maturity assertions passed. Synthetic fixtures only; production authority/publication/manual Windows acceptance NOT VERIFIED.");
    }
}
