using System.Globalization;
using System.Text;
using Npgsql;

namespace SyntheticEvaluationWorkflow;

internal static class SyntheticEvaluationWorkflowMigration
{
    internal const string Id = "synthetic-evaluation-workflow-001";
    internal const long Lock = 734021014;
    private static string Script
    {
        get
        {
            using var stream = typeof(SyntheticEvaluationWorkflowMigration).Assembly.GetManifestResourceStream("SyntheticEvaluationWorkflow.001-initial.sql")
                ?? throw new InvalidOperationException("Embedded migration unavailable.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
    internal static async Task LockAsync(NpgsqlConnection c, NpgsqlTransaction t, CancellationToken ct) =>
        await Execute(c, t, "SELECT pg_advisory_xact_lock(734021014)", ct);
    internal static async Task InitializeAsync(NpgsqlConnection c, CancellationToken ct)
    {
        await using var t = await c.BeginTransactionAsync(ct);
        await LockAsync(c, t, ct);
        await using (var exists = new NpgsqlCommand("SELECT count(*) FROM pg_namespace WHERE nspname='synthetic_evaluation_workflow'", c, t))
        {
            if (Convert.ToInt64(await exists.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture) != 0)
            {
                try { await VerifyAsync(c, t, ct); }
                catch (WorkflowInvalidException) { throw new WorkflowInvalidException(EvaluationWorkflowIssue.MigrationDrift); }
                await t.CommitAsync(ct);
                return;
            }
        }
        await Execute(c, t, "CREATE SCHEMA synthetic_evaluation_workflow; CREATE TABLE synthetic_evaluation_workflow.schema_migrations (migration_id text PRIMARY KEY,script_digest text NOT NULL,schema_fingerprint text NOT NULL,applied_at timestamptz NOT NULL)", ct);
        await Execute(c, t, Script, ct);
        var fingerprint = await Fingerprint(c, t, ct);
        await Execute(c, t, "INSERT INTO synthetic_evaluation_workflow.schema_migrations VALUES(@id,@digest,@fingerprint,clock_timestamp())", ct,
            ("id", Id), ("digest", EvaluationWorkflowCanonical.Hash(Script)), ("fingerprint", fingerprint));
        await t.CommitAsync(ct);
    }
    internal static async Task VerifyAsync(NpgsqlConnection c, NpgsqlTransaction t, CancellationToken ct)
    {
        try
        {
            await using (var exists = new NpgsqlCommand("SELECT count(*) FROM pg_namespace WHERE nspname='synthetic_evaluation_workflow'", c, t))
                if (Convert.ToInt64(await exists.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture) == 0)
                    throw new WorkflowInvalidException(EvaluationWorkflowIssue.NotInitialized);
            await MetadataShape(c, t, ct);
            var history = await History(c, t, ct);
            if (history.Count != 1 || history[0].Id != Id || history[0].Digest != EvaluationWorkflowCanonical.Hash(Script)
                || history[0].Fingerprint != await Fingerprint(c, t, ct))
                throw new WorkflowInvalidException(EvaluationWorkflowIssue.MigrationDrift);
        }
        catch (PostgresException e) when (e.SqlState is PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.InvalidSchemaName)
        { throw new WorkflowInvalidException(EvaluationWorkflowIssue.NotInitialized); }
    }
    private static async Task MetadataShape(NpgsqlConnection c, NpgsqlTransaction t, CancellationToken ct)
    {
        await using var columns = new NpgsqlCommand("""
          SELECT column_name,udt_name,is_nullable,column_default
          FROM information_schema.columns WHERE table_schema='synthetic_evaluation_workflow'
          AND table_name='schema_migrations' ORDER BY ordinal_position
          """, c, t);
        var expected = new[] { ("migration_id", "text"), ("script_digest", "text"), ("schema_fingerprint", "text"), ("applied_at", "timestamptz") };
        await using (var r = await columns.ExecuteReaderAsync(ct))
        {
            var index = 0;
            while (await r.ReadAsync(ct))
            {
                if (index >= expected.Length || r.GetString(0) != expected[index].Item1 || r.GetString(1) != expected[index].Item2 ||
                    r.GetString(2) != "NO" || !r.IsDBNull(3))
                    throw new WorkflowInvalidException(EvaluationWorkflowIssue.MigrationDrift);
                index++;
            }
            if (index != expected.Length) throw new WorkflowInvalidException(EvaluationWorkflowIssue.MigrationDrift);
        }
        await using var metadata = new NpgsqlCommand("""
          SELECT count(*),coalesce(string_agg(pg_get_constraintdef(n.oid),''),'')
          FROM pg_constraint n JOIN pg_class c ON c.oid=n.conrelid JOIN pg_namespace s ON s.oid=c.relnamespace
          WHERE s.nspname='synthetic_evaluation_workflow' AND c.relname='schema_migrations'
          """, c, t);
        await using (var r = await metadata.ExecuteReaderAsync(ct))
        {
            await r.ReadAsync(ct);
            if (r.GetInt64(0) != 1 || r.GetString(1) != "PRIMARY KEY (migration_id)")
                throw new WorkflowInvalidException(EvaluationWorkflowIssue.MigrationDrift);
        }
    }
    private static async Task<List<(string Id, string Digest, string Fingerprint)>> History(NpgsqlConnection c, NpgsqlTransaction t, CancellationToken ct)
    {
        var rows = new List<(string, string, string)>();
        await using var cmd = new NpgsqlCommand("SELECT migration_id,script_digest,schema_fingerprint FROM synthetic_evaluation_workflow.schema_migrations", c, t);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) rows.Add((r.GetString(0), r.GetString(1), r.GetString(2)));
        return rows;
    }
    private static async Task<string> Fingerprint(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        const string query = """
            SELECT definition FROM (
                SELECT 'column|'||table_name||'|'||column_name||'|'||udt_name||'|'||is_nullable||'|'||coalesce(column_default,'') AS definition
                    FROM information_schema.columns WHERE table_schema='synthetic_evaluation_workflow'
                UNION ALL SELECT 'attribute|'||c.relname||'|'||a.attnum::text||'|'||a.attname||'|'||a.atttypid::text||'|'||a.atttypmod::text||'|'||a.attcollation::text||'|'||a.attnotnull::text||'|'||a.attidentity::text||'|'||a.attgenerated::text||'|'||a.attisdropped::text
                    FROM pg_attribute a JOIN pg_class c ON c.oid=a.attrelid JOIN pg_namespace s ON s.oid=c.relnamespace
                    WHERE s.nspname='synthetic_evaluation_workflow' AND a.attnum>0
                UNION ALL SELECT 'relation|'||c.relname||'|'||c.relkind::text||'|'||c.relrowsecurity::text||'|'||c.relforcerowsecurity::text
                    FROM pg_class c JOIN pg_namespace s ON s.oid=c.relnamespace WHERE s.nspname='synthetic_evaluation_workflow'
                UNION ALL SELECT 'type|'||t.typname||'|'||t.typtype::text||'|'||coalesce(pg_get_expr(t.typdefaultbin,0),'')
                    FROM pg_type t JOIN pg_namespace s ON s.oid=t.typnamespace WHERE s.nspname='synthetic_evaluation_workflow'
                UNION ALL SELECT 'constraint|'||c.relname||'|'||n.conname||'|'||pg_get_constraintdef(n.oid)
                    FROM pg_constraint n JOIN pg_class c ON c.oid=n.conrelid JOIN pg_namespace s ON s.oid=c.relnamespace WHERE s.nspname='synthetic_evaluation_workflow'
                UNION ALL SELECT 'index|'||tablename||'|'||indexdef FROM pg_indexes WHERE schemaname='synthetic_evaluation_workflow'
                UNION ALL SELECT 'trigger|'||c.relname||'|'||t.tgenabled::text||'|'||pg_get_triggerdef(t.oid)
                    FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid JOIN pg_namespace s ON s.oid=c.relnamespace WHERE s.nspname='synthetic_evaluation_workflow'
                UNION ALL SELECT 'function|'||p.proname||'|'||pg_get_functiondef(p.oid)
                    FROM pg_proc p JOIN pg_namespace s ON s.oid=p.pronamespace WHERE s.nspname='synthetic_evaluation_workflow'
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
        return EvaluationWorkflowCanonical.Hash(values.ToString());
    }

    internal static async Task Execute(NpgsqlConnection c, NpgsqlTransaction t, string sql, CancellationToken ct, params (string Name, object Value)[] parameters)
    {
        await using var cmd = new NpgsqlCommand(sql, c, t);
        foreach (var p in parameters) cmd.Parameters.AddWithValue(p.Name, p.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
