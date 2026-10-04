using System.Globalization;
using System.Text;
using Npgsql;

namespace FindingReview;

public sealed class SyntheticReviewMigrationException(string message) : Exception(message);
internal static class SyntheticReviewMigration
{
    internal const string Id = "synthetic-review-001";
    private static string Script
    {
        get
        {
            using var stream = typeof(SyntheticReviewMigration).Assembly.GetManifestResourceStream("FindingReview.001-initial.sql")
                ?? throw new InvalidOperationException("Embedded review migration missing.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
    internal static async Task InitializeAsync(NpgsqlConnection connection, SyntheticReviewScope scope, CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Execute(connection, transaction, "SELECT pg_advisory_xact_lock(734021005)", cancellationToken);
        await Execute(connection, transaction, """
            CREATE SCHEMA IF NOT EXISTS synthetic_review;
            CREATE TABLE IF NOT EXISTS synthetic_review.schema_migrations (
                migration_id text PRIMARY KEY, digest text NOT NULL, schema_fingerprint text NOT NULL,
                applied_at timestamptz NOT NULL);
            """, cancellationToken);
        var rows = await History(connection, transaction, cancellationToken);
        var digest = SyntheticReviewDigest.HashText(Script);
        if (rows.Count > 1 || rows.Count == 1 && (rows[0].Id != Id || rows[0].Digest != digest))
            throw new SyntheticReviewMigrationException("Review migration history/content drift refused.");
        if (rows.Count == 0)
        {
            await using var count = new NpgsqlCommand("SELECT count(*) FROM information_schema.tables WHERE table_schema='synthetic_review'", connection, transaction);
            if (Convert.ToInt64(await count.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) != 1)
                throw new SyntheticReviewMigrationException("Untracked review schema refused.");
            await Execute(connection, transaction, Script, cancellationToken);
            var fingerprint = await Fingerprint(connection, transaction, cancellationToken);
            await Execute(connection, transaction, "INSERT INTO synthetic_review.schema_migrations VALUES (@id,@digest,@fingerprint,clock_timestamp())", cancellationToken,
                ("id", Id), ("digest", digest), ("fingerprint", fingerprint));
        }
        else if (rows[0].Fingerprint != await Fingerprint(connection, transaction, cancellationToken))
            throw new SyntheticReviewMigrationException("Review schema drift refused.");
        await Execute(connection, transaction, "INSERT INTO synthetic_review.data_plane_scope VALUES (true,@customer,@project,@environment) ON CONFLICT DO NOTHING", cancellationToken,
            ("customer", scope.CustomerId), ("project", scope.ProjectId), ("environment", scope.EnvironmentId));
        if (await Binding(connection, transaction, scope, cancellationToken) is not null)
            throw new SyntheticReviewMigrationException("Review schema scope binding differs.");
        await transaction.CommitAsync(cancellationToken);
    }

    internal static async Task<SyntheticReviewIssue?> VerifyAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        SyntheticReviewScope scope, CancellationToken cancellationToken)
    {
        try
        {
            var rows = await History(connection, transaction, cancellationToken);
            if (rows.Count != 1 || rows[0].Id != Id || rows[0].Digest != SyntheticReviewDigest.HashText(Script) ||
                rows[0].Fingerprint != await Fingerprint(connection, transaction, cancellationToken)) return SyntheticReviewIssue.MigrationDrift;
            return await Binding(connection, transaction, scope, cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState is PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.InvalidSchemaName)
        {
            return SyntheticReviewIssue.NotInitialized;
        }
    }
    private static async Task<SyntheticReviewIssue?> Binding(NpgsqlConnection connection, NpgsqlTransaction transaction,
        SyntheticReviewScope scope, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT customer_id,project_id,environment_id FROM synthetic_review.data_plane_scope WHERE singleton=true", connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return SyntheticReviewIssue.NotInitialized;
        return reader.GetString(0) == scope.CustomerId && reader.GetString(1) == scope.ProjectId && reader.GetString(2) == scope.EnvironmentId
            ? null : SyntheticReviewIssue.WrongScope;
    }
    private static async Task<List<(string Id, string Digest, string Fingerprint)>> History(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        var records = new List<(string, string, string)>();
        await using var command = new NpgsqlCommand("SELECT migration_id,digest,schema_fingerprint FROM synthetic_review.schema_migrations", connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) records.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        return records;
    }
    private static async Task<string> Fingerprint(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        const string query = """
            SELECT definition FROM (
                SELECT 'column|'||table_name||'|'||column_name||'|'||udt_name||'|'||is_nullable||'|'||coalesce(column_default,'') AS definition
                    FROM information_schema.columns WHERE table_schema='synthetic_review'
                UNION ALL SELECT 'constraint|'||c.relname||'|'||n.conname||'|'||pg_get_constraintdef(n.oid)
                    FROM pg_constraint n JOIN pg_class c ON c.oid=n.conrelid JOIN pg_namespace s ON s.oid=c.relnamespace WHERE s.nspname='synthetic_review'
                UNION ALL SELECT 'index|'||tablename||'|'||indexdef FROM pg_indexes WHERE schemaname='synthetic_review'
                UNION ALL SELECT 'trigger|'||c.relname||'|'||t.tgenabled::text||'|'||pg_get_triggerdef(t.oid)
                    FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid JOIN pg_namespace s ON s.oid=c.relnamespace WHERE s.nspname='synthetic_review' AND NOT t.tgisinternal
                UNION ALL SELECT 'function|'||p.proname||'|'||pg_get_functiondef(p.oid)
                    FROM pg_proc p JOIN pg_namespace s ON s.oid=p.pronamespace WHERE s.nspname='synthetic_review'
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
        return SyntheticReviewDigest.HashText(values.ToString());
    }
    internal static async Task Execute(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
