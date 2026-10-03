using System.Text.Json;
using System.Text.Json.Serialization;

internal static class V13Program
{
    internal static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.SequenceEqual(new[] { "--restore-host-schema" }))
            {
                await HostSchemaCases.Restore();
                Console.WriteLine($"PASS V13 outer owned schema restoration; checks={Check.Count}");
                return 0;
            }
            if (args.Length == 2 && args[0] == "--host-schema-denial" && Guid.TryParseExact(args[1], "D", out var run) && run != Guid.Empty && args[1] == run.ToString("D"))
            {
                await HostSchemaCases.Run(run);
                Console.WriteLine($"PASS V13 owned host schema denial; checks={Check.Count}");
                return 0;
            }
            if (args.Length > 1 || (args.Length == 1 && args[0] != "--portable")) throw new InvalidOperationException("invalid_cli");
            PortableCases.Run();
            PolicyCases.Run();
            if (args.Length == 0) await DatabaseCases.Run();
            else Console.WriteLine("NOT VERIFIED AR13-T04/05/06/12 PG: portable mode excludes durable verification.");
            Console.WriteLine($"PASS V13 independent artifact review; checks={Check.Count}");
            return 0;
        }
        catch (Exception exception)
        {
            if (exception is Npgsql.PostgresException postgres) Console.Error.WriteLine("POSTGRES-SQLSTATE " + postgres.SqlState);
            Console.Error.WriteLine($"FAIL V13; code={Check.Last}; checks={Check.Count}; exception={exception.GetType().Name}; payload-suppressed");
            return 1;
        }
    }
}
