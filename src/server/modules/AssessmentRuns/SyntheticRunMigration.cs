using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace AssessmentRuns;

public sealed class SyntheticMigrationDriftException(string message) : Exception(message);

internal static class SyntheticRunMigration
{
    internal const string MigrationId = "synthetic-assessment-runs-001";

    internal static async Task InitializeAsync(SyntheticRunDbContext db, CancellationToken cancellationToken)
    {
        var script = ReadScript();
        var digest = Hash(script);
        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var nativeTransaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        await ExecuteAsync(connection, nativeTransaction, "SELECT pg_advisory_xact_lock(734021003)", cancellationToken);
        await ExecuteAsync(connection, nativeTransaction, """
            CREATE SCHEMA IF NOT EXISTS synthetic_assessment;
            CREATE TABLE IF NOT EXISTS synthetic_assessment.schema_migrations (
                migration_id text PRIMARY KEY, digest text NOT NULL,
                schema_fingerprint text NOT NULL, applied_at timestamp with time zone NOT NULL);
            """, cancellationToken);
        await using var history = new NpgsqlCommand("SELECT migration_id,digest,schema_fingerprint FROM synthetic_assessment.schema_migrations", connection, nativeTransaction);
        var records = new List<(string Id, string Digest, string Fingerprint)>();
        await using (var reader = await history.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken)) records.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        }
        if (records.Count > 1 || (records.Count == 1 && (records[0].Id != MigrationId || records[0].Digest != digest)))
        {
            throw new SyntheticMigrationDriftException("Synthetic migration history or content digest differs; migration refused.");
        }
        if (records.Count == 0)
        {
            await using var count = new NpgsqlCommand("SELECT count(*) FROM information_schema.tables WHERE table_schema='synthetic_assessment'", connection, nativeTransaction);
            if (Convert.ToInt64(await count.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture) != 1)
                throw new SyntheticMigrationDriftException("Untracked synthetic schema already exists; migration refused.");
            await ExecuteAsync(connection, nativeTransaction, script, cancellationToken);
            var fingerprint = await FingerprintAsync(connection, nativeTransaction, cancellationToken);
            await using var insert = new NpgsqlCommand("INSERT INTO synthetic_assessment.schema_migrations VALUES (@id,@digest,@fingerprint,clock_timestamp())", connection, nativeTransaction);
            insert.Parameters.AddWithValue("id", MigrationId);
            insert.Parameters.AddWithValue("digest", digest);
            insert.Parameters.AddWithValue("fingerprint", fingerprint);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        else if (records[0].Fingerprint != await FingerprintAsync(connection, nativeTransaction, cancellationToken))
        {
            throw new SyntheticMigrationDriftException("Synthetic database schema drift detected; migration refused.");
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<string> FingerprintAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        const string query = """
            SELECT definition FROM (
                SELECT 'column|'||table_name||'|'||column_name||'|'||udt_name||'|'||is_nullable||'|'||coalesce(column_default,'') AS definition
                    FROM information_schema.columns WHERE table_schema='synthetic_assessment'
                UNION ALL SELECT 'constraint|'||c.relname||'|'||n.conname||'|'||pg_get_constraintdef(n.oid)
                    FROM pg_constraint n JOIN pg_class c ON c.oid=n.conrelid JOIN pg_namespace s ON s.oid=c.relnamespace
                    WHERE s.nspname='synthetic_assessment'
                UNION ALL SELECT 'index|'||tablename||'|'||indexdef FROM pg_indexes WHERE schemaname='synthetic_assessment'
                UNION ALL SELECT 'trigger|'||c.relname||'|'||t.tgenabled::text||'|'||pg_get_triggerdef(t.oid)
                    FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid JOIN pg_namespace s ON s.oid=c.relnamespace
                    WHERE s.nspname='synthetic_assessment' AND NOT t.tgisinternal
                UNION ALL SELECT 'function|'||p.proname||'|'||pg_get_functiondef(p.oid)
                    FROM pg_proc p JOIN pg_namespace s ON s.oid=p.pronamespace WHERE s.nspname='synthetic_assessment'
            ) AS definitions ORDER BY definition COLLATE "C"
            """;
        var values = new StringBuilder();
        await using var command = new NpgsqlCommand(query, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var value = reader.GetString(0);
            values.Append(value.Length).Append(':').Append(value);
        }
        return Hash(values.ToString());
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string ReadScript()
    {
        using var stream = typeof(SyntheticRunMigration).Assembly.GetManifestResourceStream("AssessmentRuns.001-initial.sql")
            ?? throw new InvalidOperationException("Embedded synthetic migration is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    internal static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
