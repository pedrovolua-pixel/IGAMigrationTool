internal static class Program
{
    private static async Task Main(string[] args)
    {
        CoreCases.Run();
        await PersistenceCases.Run(args);
        Console.WriteLine($"{Check.Count} independent guidance assertions passed. Inert synthetic unverified values only; manual Windows, live pilot, CSV/PDF acceptance NOT VERIFIED.");
    }
}
