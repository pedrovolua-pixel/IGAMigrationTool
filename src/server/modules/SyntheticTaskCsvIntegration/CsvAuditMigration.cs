using System.Globalization;
using System.Text;
using Npgsql;
using SyntheticTaskCsv;

namespace SyntheticTaskCsvIntegration;

public sealed class CsvAuditMigrationException(string message) : Exception(message);
internal static class CsvAuditMigration
{
    private static string Hash(string value) => CsvCanonical.Hash(Encoding.UTF8.GetBytes(value));
    internal const string Id = "synthetic-task-csv-001";
    private static string Script
    {
        get
        {
            using var stream = typeof(CsvAuditMigration).Assembly.GetManifestResourceStream("SyntheticTaskCsvIntegration.001-initial.sql")
                ?? throw new InvalidOperationException("Embedded planning task migration missing.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
    internal static async Task InitializeAsync(NpgsqlConnection connection, CsvScope scope, CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Execute(connection, transaction, "SELECT pg_advisory_xact_lock(734021015)", cancellationToken);
        await Execute(connection, transaction, """
            CREATE SCHEMA IF NOT EXISTS synthetic_task_csv;
            CREATE TABLE IF NOT EXISTS synthetic_task_csv.schema_migrations (
                migration_id text PRIMARY KEY, digest text NOT NULL, schema_fingerprint text NOT NULL,
                applied_at timestamptz NOT NULL);
            """, cancellationToken);
        var rows = await History(connection, transaction, cancellationToken);
        var digest = Hash(Script);
        if (rows.Count > 1 || rows.Count == 1 && (rows[0].Id != Id || rows[0].Digest != digest))
            throw new CsvAuditMigrationException("Task migration history/content drift refused.");
        if (rows.Count == 0)
        {
            await using var count = new NpgsqlCommand("SELECT count(*) FROM information_schema.tables WHERE table_schema='synthetic_task_csv'", connection, transaction);
            if (Convert.ToInt64(await count.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) != 1)
                throw new CsvAuditMigrationException("Untracked review schema refused.");
            await using var unknown = new NpgsqlCommand("""
                SELECT (SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                    WHERE n.nspname='synthetic_task_csv' AND c.relname NOT IN ('schema_migrations','schema_migrations_pkey'))
                  + (SELECT count(*) FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname='synthetic_task_csv')
                  + (SELECT count(*) FROM pg_type t JOIN pg_namespace n ON n.oid=t.typnamespace
                    WHERE n.nspname='synthetic_task_csv' AND t.typname NOT IN ('schema_migrations','_schema_migrations'))
                """, connection, transaction);
            if (Convert.ToInt64(await unknown.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) != 0)
                throw new CsvAuditMigrationException("Unknown planning task schema objects refused.");
            await Execute(connection, transaction, Script, cancellationToken);
            var fingerprint = await Fingerprint(connection, transaction, cancellationToken);
            await Execute(connection, transaction, "INSERT INTO synthetic_task_csv.schema_migrations VALUES (@id,@digest,@fingerprint,clock_timestamp())", cancellationToken,
                ("id", Id), ("digest", digest), ("fingerprint", fingerprint));
        }
        else if (rows[0].Fingerprint != await Fingerprint(connection, transaction, cancellationToken))
            throw new CsvAuditMigrationException("Task schema drift refused.");
        await Execute(connection, transaction, "INSERT INTO synthetic_task_csv.data_plane_scope VALUES (true,@customer,@project,@environment) ON CONFLICT DO NOTHING", cancellationToken,
            ("customer", scope.CustomerId), ("project", scope.ProjectId), ("environment", scope.EnvironmentId));
        if (await Binding(connection, transaction, scope, cancellationToken) is not null)
            throw new CsvAuditMigrationException("Task schema scope binding differs.");
        await transaction.CommitAsync(cancellationToken);
    }

    internal static async Task<CsvIssue?> VerifyAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        CsvScope scope, CancellationToken cancellationToken)
    {
        try
        {
            var rows = await History(connection, transaction, cancellationToken);
            if (rows.Count != 1 || rows[0].Id != Id || rows[0].Digest != Hash(Script) ||
                rows[0].Fingerprint != await Fingerprint(connection, transaction, cancellationToken)) return CsvIssue.IntegrityMismatch;
            return await Binding(connection, transaction, scope, cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState is PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.InvalidSchemaName)
        {
            return CsvIssue.AuditUnavailable;
        }
    }
    private static async Task<CsvIssue?> Binding(NpgsqlConnection connection, NpgsqlTransaction transaction,
        CsvScope scope, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT customer_id,project_id,environment_id FROM synthetic_task_csv.data_plane_scope WHERE singleton=true", connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return CsvIssue.AuditUnavailable;
        return reader.GetString(0) == scope.CustomerId && reader.GetString(1) == scope.ProjectId && reader.GetString(2) == scope.EnvironmentId
            ? null : CsvIssue.WrongScope;
    }
    private static async Task<List<(string Id, string Digest, string Fingerprint)>> History(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        var records = new List<(string, string, string)>();
        await using var command = new NpgsqlCommand("SELECT migration_id,digest,schema_fingerprint FROM synthetic_task_csv.schema_migrations", connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) records.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        return records;
    }
    private static async Task<string> Fingerprint(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        const string query = """
            SELECT definition FROM (
                SELECT 'column|'||table_name||'|'||column_name||'|'||udt_name||'|'||is_nullable||'|'||coalesce(column_default,'') AS definition
                    FROM information_schema.columns WHERE table_schema='synthetic_task_csv'
                UNION ALL SELECT 'attribute|'||c.relname||'|'||a.attnum::text||'|'||a.attname||'|'||a.atttypid::text||'|'||a.atttypmod::text||'|'||a.attcollation::text||'|'||a.attnotnull::text||'|'||a.attidentity::text||'|'||a.attgenerated::text||'|'||a.attisdropped::text
                    FROM pg_attribute a JOIN pg_class c ON c.oid=a.attrelid JOIN pg_namespace s ON s.oid=c.relnamespace
                    WHERE s.nspname='synthetic_task_csv' AND a.attnum>0
                UNION ALL SELECT 'relation|'||c.relname||'|'||c.relkind::text||'|'||c.relrowsecurity::text||'|'||c.relforcerowsecurity::text
                    FROM pg_class c JOIN pg_namespace s ON s.oid=c.relnamespace WHERE s.nspname='synthetic_task_csv'
                UNION ALL SELECT 'type|'||t.typname||'|'||t.typtype::text||'|'||coalesce(pg_get_expr(t.typdefaultbin,0),'')
                    FROM pg_type t JOIN pg_namespace s ON s.oid=t.typnamespace WHERE s.nspname='synthetic_task_csv'
                UNION ALL SELECT 'constraint|'||c.relname||'|'||n.conname||'|'||pg_get_constraintdef(n.oid)
                    FROM pg_constraint n JOIN pg_class c ON c.oid=n.conrelid JOIN pg_namespace s ON s.oid=c.relnamespace WHERE s.nspname='synthetic_task_csv'
                UNION ALL SELECT 'index|'||tablename||'|'||indexdef FROM pg_indexes WHERE schemaname='synthetic_task_csv'
                UNION ALL SELECT 'trigger|'||c.relname||'|'||t.tgenabled::text||'|'||pg_get_triggerdef(t.oid)
                    FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid JOIN pg_namespace s ON s.oid=c.relnamespace WHERE s.nspname='synthetic_task_csv'
                UNION ALL SELECT 'function|'||p.proname||'|'||pg_get_functiondef(p.oid)
                    FROM pg_proc p JOIN pg_namespace s ON s.oid=p.pronamespace WHERE s.nspname='synthetic_task_csv'
            ) AS definitions ORDER BY definition COLLATE "C"
            """;
        var values = new StringBuilder();
        await using var command = new NpgsqlCommand(query, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var value = reader.GetString(0);
            values.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
        }
        return Hash(values.ToString());
    }
    internal static async Task Execute(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
