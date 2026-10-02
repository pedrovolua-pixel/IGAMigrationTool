using System.Text.Json;

internal static class V12Program
{
    internal static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    internal static readonly JsonSerializerOptions SourceWeb = new(Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
    private static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length > 1 || (args.Length == 1 && args[0] != "--portable")) throw new InvalidOperationException("invalid_cli");
            PortableCases.Run();
            if (args.Length == 0) await DatabaseCases.Run();
            else Console.WriteLine("NOT VERIFIED V12-PG: --portable excludes saved PostgreSQL source/review checks.");
            Console.WriteLine($"PASS V12 independent local fix packages; checks={Check.Count}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("STACK-CODES " + string.Join("/", new System.Diagnostics.StackTrace(exception).GetFrames().Take(8).Select(f => f.GetMethod()?.DeclaringType?.Name + "." + f.GetMethod()?.Name)));
            if (exception is Npgsql.PostgresException postgres) Console.Error.WriteLine("POSTGRES-SQLSTATE " + postgres.SqlState);
            Console.Error.WriteLine($"FAIL V12; code={Check.Last}; checks={Check.Count}; exception={exception.GetType().Name}; payload-suppressed");
            return 1;
        }
    }
}
