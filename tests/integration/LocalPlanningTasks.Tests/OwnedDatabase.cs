using System.Globalization;
using Npgsql;

internal static class OwnedDatabase
{
    internal const string DomainName = "iga_synthetic_cycle14_v14_domain";
    internal const string BrowserName = "iga_synthetic_cycle14_v14_browser";
    internal static string Connection { get; private set; } = "";
    internal static void Configure(string expectedName = DomainName)
    {
        Connection = Environment.GetEnvironmentVariable("IGA_PLANNING_TASK_TEST_DATABASE") ??
            "Host=127.0.0.1;Port=55433;Database=iga_synthetic_cycle14_v14_domain;Username=iga_synthetic";
        Guard(Connection, expectedName);
    }
    internal static void Guard(string value, string expectedName = DomainName)
    {
        if (expectedName is not (DomainName or BrowserName)) throw new InvalidOperationException("owned_database_name_guard");
        var parts = value.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split('=', 2)).ToArray();
        if (parts.Any(p => p.Length != 2)) throw new InvalidOperationException("owned_database_syntax");
        var keys = parts.Select(p => p[0].Trim().ToLowerInvariant()).ToArray();
        if (keys.Distinct(StringComparer.Ordinal).Count() != keys.Length ||
            !keys.Order(StringComparer.Ordinal).SequenceEqual(new[] { "database", "host", "port", "username" }))
            throw new InvalidOperationException("owned_database_duplicate_extra_key");
        var settings = new NpgsqlConnectionStringBuilder(value);
        if (settings.Host != "127.0.0.1" || settings.Port != 55433 || settings.Database != expectedName || settings.Username != "iga_synthetic")
            throw new InvalidOperationException("owned_database_guard");
    }
    internal static async Task CreateAbsentOnly()
    {
        Guard(Connection);
        await using var db = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(Connection) { Database = "postgres" }.ConnectionString);
        await db.OpenAsync();
        await using var query = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname=@name", db);
        query.Parameters.AddWithValue("name", DomainName);
        if (Convert.ToInt32(await query.ExecuteScalarAsync(), CultureInfo.InvariantCulture) == 0)
        {
            await using var create = new NpgsqlCommand("CREATE DATABASE iga_synthetic_cycle14_v14_domain", db);
            await create.ExecuteNonQueryAsync();
        }
    }
    internal static async Task<T?> Scalar<T>(string sql, params (string Name, object Value)[] parameters)
    {
        Guard(Connection);
        await using var db = new NpgsqlConnection(Connection); await db.OpenAsync();
        await using var command = new NpgsqlCommand(sql, db);
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        return (T?)await command.ExecuteScalarAsync();
    }
    internal static async Task Sql(string sql, params (string Name, object Value)[] parameters)
    {
        Guard(Connection);
        await using var db = new NpgsqlConnection(Connection); await db.OpenAsync();
        await using var command = new NpgsqlCommand(sql, db);
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await command.ExecuteNonQueryAsync();
    }
    internal static async Task<string> FullRows(Guid run)
    {
        // Full observed row bytes, never an expected-value oracle. Comparison is
        // stronger than counts for no-write replay/duplicate/denial assertions.
        var result = new List<string>();
        foreach (var (schema, table) in new[]
        {
            ("synthetic_planning_tasks", "source_versions"), ("synthetic_planning_tasks", "task_seeds"),
            ("synthetic_planning_tasks", "task_current"), ("synthetic_planning_tasks", "events"), ("synthetic_planning_tasks", "receipts"),
            ("synthetic_fix_review", "source_versions"), ("synthetic_fix_review", "artifact_seeds"),
            ("synthetic_fix_review", "artifact_current"), ("synthetic_fix_review", "events"), ("synthetic_fix_review", "receipts")
        })
            result.Add(await Scalar<string>("SELECT COALESCE(jsonb_agg(to_jsonb(t) ORDER BY to_jsonb(t)::text)::text,'[]') FROM " + schema + "." + table + " t WHERE run_id=@run", ("run", run)) ?? "[]");
        return string.Join("\n", result);
    }
}
