using System.Globalization;
using Npgsql;

namespace SyntheticAiExecution;

internal static class AiExecutionMigration
{
    internal const string Id = "synthetic-ai-execution-001";
    internal static string Script
    {
        get
        {
            using var stream = typeof(AiExecutionMigration).Assembly.GetManifestResourceStream("SyntheticAiExecution.001-initial.sql") ?? throw new InvalidOperationException("AI migration unavailable.");
            using var reader = new StreamReader(stream); return reader.ReadToEnd();
        }
    }
    internal static async Task Initialize(NpgsqlConnection connection, CancellationToken ct)
    {
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await Execute(connection, transaction, "SELECT pg_advisory_xact_lock(734021015)", ct);
        await Execute(connection, transaction, "CREATE SCHEMA IF NOT EXISTS synthetic_ai_execution; CREATE TABLE IF NOT EXISTS synthetic_ai_execution.schema_migrations (migration_id text PRIMARY KEY,digest text NOT NULL,fingerprint text NOT NULL)", ct);
        await using var read = new NpgsqlCommand("SELECT migration_id,digest,fingerprint FROM synthetic_ai_execution.schema_migrations", connection, transaction);
        var history = new List<(string, string, string)>();
        await using (var reader = await read.ExecuteReaderAsync(ct)) while (await reader.ReadAsync(ct)) history.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        if (history.Count == 0)
        {
            await using var check = new NpgsqlCommand("SELECT count(*) FROM pg_class c JOIN pg_namespace n ON c.relnamespace=n.oid WHERE n.nspname='synthetic_ai_execution' AND c.relname NOT IN ('schema_migrations','schema_migrations_pkey')", connection, transaction);
            if (Convert.ToInt64(await check.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture) != 0) throw new InvalidOperationException("AI untracked schema denied.");
            await Execute(connection, transaction, Script, ct);
            await Execute(connection, transaction, "INSERT INTO synthetic_ai_execution.scope VALUES(true,@c,@p,@e)", ct, ("c", AiScope.Fixed.CustomerId), ("p", AiScope.Fixed.ProjectId), ("e", AiScope.Fixed.EnvironmentId));
            await Execute(connection, transaction, "INSERT INTO synthetic_ai_execution.schema_migrations VALUES(@id,@d,@f)", ct, ("id", Id), ("d", AiExecutionCanonical.Hash(Script)), ("f", await Fingerprint(connection, transaction, ct)));
        }
        else if (history.Count != 1 || history[0].Item1 != Id || history[0].Item2 != AiExecutionCanonical.Hash(Script) || history[0].Item3 != await Fingerprint(connection, transaction, ct))
            throw new InvalidOperationException("AI migration drift denied.");
        if (await Verify(connection, transaction, ct) is not null) throw new InvalidOperationException("AI scope/drift denied.");
        await transaction.CommitAsync(ct);
    }
    internal static async Task<AiIssue?> Verify(NpgsqlConnection c, NpgsqlTransaction t, CancellationToken ct)
    {
        try
        {
            await using var check = new NpgsqlCommand("SELECT migration_id,digest,fingerprint FROM synthetic_ai_execution.schema_migrations", c, t);
            await using (var reader = await check.ExecuteReaderAsync(ct))
            {
                if (!await reader.ReadAsync(ct) || reader.GetString(0) != Id || reader.GetString(1) != AiExecutionCanonical.Hash(Script)) return AiIssue.MigrationDrift;
                var fingerprint = reader.GetString(2); if (await reader.ReadAsync(ct)) return AiIssue.MigrationDrift;
                await reader.CloseAsync();
                if (fingerprint != await Fingerprint(c, t, ct)) return AiIssue.MigrationDrift;
            }
            await using var scope = new NpgsqlCommand("SELECT customer_id,project_id,environment_id FROM synthetic_ai_execution.scope WHERE singleton=true", c, t);
            await using var row = await scope.ExecuteReaderAsync(ct);
            if (!await row.ReadAsync(ct)) return AiIssue.NotInitialized;
            return row.GetString(0) == AiScope.Fixed.CustomerId && row.GetString(1) == AiScope.Fixed.ProjectId && row.GetString(2) == AiScope.Fixed.EnvironmentId ? null : AiIssue.WrongScope;
        }
        catch (PostgresException x) when (x.SqlState is PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.InvalidSchemaName) { return AiIssue.NotInitialized; }
    }
    private static async Task<string> Fingerprint(NpgsqlConnection c, NpgsqlTransaction t, CancellationToken ct)
    {
        const string sql = """
            SELECT v FROM (
              SELECT 'col|'||table_name||'|'||column_name||'|'||ordinal_position||'|'||udt_name||'|'||is_nullable||'|'||coalesce(column_default,'') v FROM information_schema.columns WHERE table_schema='synthetic_ai_execution'
              UNION ALL SELECT 'rel|'||c.relname||'|'||c.relkind||'|'||c.relrowsecurity::text||'|'||c.relforcerowsecurity::text FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='synthetic_ai_execution'
              UNION ALL SELECT 'constraint|'||r.relname||'|'||c.conname||'|'||pg_get_constraintdef(c.oid) FROM pg_constraint c JOIN pg_class r ON r.oid=c.conrelid JOIN pg_namespace n ON n.oid=r.relnamespace WHERE n.nspname='synthetic_ai_execution'
              UNION ALL SELECT 'index|'||indexdef FROM pg_indexes WHERE schemaname='synthetic_ai_execution'
              UNION ALL SELECT 'trigger|'||pg_get_triggerdef(t.oid)||'|'||t.tgenabled FROM pg_trigger t JOIN pg_class r ON r.oid=t.tgrelid JOIN pg_namespace n ON n.oid=r.relnamespace WHERE n.nspname='synthetic_ai_execution' AND NOT t.tgisinternal
              UNION ALL SELECT 'function|'||pg_get_functiondef(p.oid) FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname='synthetic_ai_execution'
            ) q ORDER BY v
            """;
        var values = new List<string>(); await using var command = new NpgsqlCommand(sql, c, t); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) values.Add(reader.GetString(0)); return AiExecutionCanonical.Hash(string.Join("\n", values));
    }
    internal static async Task Execute(NpgsqlConnection c, NpgsqlTransaction t, string sql, CancellationToken ct, params (string, object)[] values)
    {
        await using var command = new NpgsqlCommand(sql, c, t);
        foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync(ct);
    }
}
