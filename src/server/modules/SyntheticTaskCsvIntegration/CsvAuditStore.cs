using System.Globalization;
using System.Text.Json;
using Npgsql;
using SyntheticTaskCsv;

namespace SyntheticTaskCsvIntegration;

public enum CsvAuditStage { Request, Dispatch, Render, Delivery, Denial }
public enum CsvAuditReason { None, InvalidInput, Denied, SourceConflict, IntegrityMismatch, RendererFailure, LimitExceeded, AuditUnavailable, TransferInterrupted }
public sealed record CsvAuditEvent(Guid EventId, Guid RequestId, Guid RunId, string ActorId, CsvScope Scope,
    DateTimeOffset RecordedAtUtc, CsvAuditStage Stage, CsvAuditReason Reason, string? SnapshotDigest, string? OutputSha256, int? RowCount);
public sealed class CsvAuditStore
{
    private readonly string connectionString;
    public CsvAuditStore(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (builder.Host is not ("localhost" or "127.0.0.1") || builder.Port != 55433 || builder.Database is null || !builder.Database.StartsWith("iga_synthetic_phase1b_", StringComparison.Ordinal) ||
            builder.Username != "iga_synthetic") throw new ArgumentException("Fixed fictional CSV database required.");
        this.connectionString = connectionString;
    }
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await CsvAuditMigration.InitializeAsync(connection, CsvScope.Fixed, cancellationToken);
    }
    public async Task<CsvIssue?> AppendInTransactionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CsvAuditEvent audit,
        CancellationToken cancellationToken = default)
    {
        try { return await AppendCore(connection, transaction, audit, cancellationToken); }
        catch (Exception exception) when (exception is NpgsqlException or InvalidOperationException) { return CsvIssue.AuditUnavailable; }
    }
    private async Task<CsvIssue?> AppendCore(NpgsqlConnection connection, NpgsqlTransaction transaction, CsvAuditEvent audit, CancellationToken cancellationToken)
    {
        if (!Valid(audit) || !Owns(connection, transaction)) return CsvIssue.InvalidInput;
        audit = audit with { RecordedAtUtc = new DateTimeOffset(audit.RecordedAtUtc.Ticks - audit.RecordedAtUtc.Ticks % 10, TimeSpan.Zero) };
        if (await CsvAuditMigration.VerifyAsync(connection, transaction, CsvScope.Fixed, cancellationToken) is { } issue) return issue;
        await using var guard = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended(@key,0))", connection, transaction);
        guard.Parameters.AddWithValue("key", "synthetic-task-csv-request:" + audit.RequestId.ToString("D"));
        await guard.ExecuteNonQueryAsync(cancellationToken);
        var digest = CsvCanonical.Hash(CsvCanonical.Bytes(audit));
        await using var prior = new NpgsqlCommand("SELECT event_id,event_digest,stage,run_id,actor_id,snapshot_digest,output_sha256,row_count,customer_id,project_id,environment_id,recorded_at,reason,outcome,contract_version,ordinal FROM synthetic_task_csv.export_audit WHERE request_id=@request ORDER BY ordinal", connection, transaction);
        prior.Parameters.AddWithValue("request", audit.RequestId);
        var stages = new List<CsvAuditStage>(); var reasons = new List<CsvAuditReason>(); var replayFound = false;
        await using (var reader = await prior.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!Enum.TryParse<CsvAuditStage>(reader.GetString(2), out var stage) || !Enum.TryParse<CsvAuditReason>(reader.GetString(12), out var reason)) return CsvIssue.IntegrityMismatch;
                var stored = new CsvAuditEvent(reader.GetGuid(0), audit.RequestId, reader.GetGuid(3), reader.GetString(4),
                    new(reader.GetString(8), reader.GetString(9), reader.GetString(10)), new DateTimeOffset(reader.GetDateTime(11)), stage, reason,
                    reader.IsDBNull(5) ? null : reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetInt32(7));
                if (!Valid(stored) || reader.GetString(1) != CsvCanonical.Hash(CsvCanonical.Bytes(stored)) || reader.GetInt32(15) != stages.Count ||
                    reader.GetString(13) != (stage == CsvAuditStage.Denial ? "Denied" : "Accepted") || reader.GetString(14) != CsvCodec.ContractVersion) return CsvIssue.IntegrityMismatch;
                if (reader.GetGuid(0) == audit.EventId) { if (reader.GetString(1) != digest) return CsvIssue.IntegrityMismatch; replayFound = true; }
                if (reader.GetGuid(3) != audit.RunId || reader.GetString(4) != audit.ActorId) return CsvIssue.IntegrityMismatch;
                if (!reader.IsDBNull(5) && reader.GetString(5) != audit.SnapshotDigest) return CsvIssue.SourceConflict;
                if (!reader.IsDBNull(6) && audit.OutputSha256 is not null && reader.GetString(6) != audit.OutputSha256) return CsvIssue.IntegrityMismatch;
                if (!reader.IsDBNull(7) && reader.GetInt32(7) != audit.RowCount) return CsvIssue.IntegrityMismatch;
                stages.Add(stage); reasons.Add(reason);
            }
        }
        var expected = new[] { CsvAuditStage.Request, CsvAuditStage.Dispatch, CsvAuditStage.Render, CsvAuditStage.Delivery };
        var accepted = stages.TakeWhile(stage => stage != CsvAuditStage.Denial).ToArray();
        if (!accepted.SequenceEqual(expected.Take(accepted.Length)) || accepted.Length > expected.Length ||
            stages.Skip(accepted.Length).Any(stage => stage != CsvAuditStage.Denial) || stages.Count(stage => stage == CsvAuditStage.Denial) > 1 ||
            stages.Count > accepted.Length && (reasons[^1] == CsvAuditReason.TransferInterrupted) != (accepted.Length == expected.Length)) return CsvIssue.IntegrityMismatch;
        if (replayFound) return null;
        var interruption = audit.Stage == CsvAuditStage.Denial && audit.Reason == CsvAuditReason.TransferInterrupted;
        if (stages.Contains(CsvAuditStage.Denial) || stages.Contains(CsvAuditStage.Delivery) != interruption ||
            !stages.SequenceEqual(expected.Take(stages.Count)) || audit.Stage != CsvAuditStage.Denial && (stages.Count >= expected.Length || audit.Stage != expected[stages.Count])) return CsvIssue.IntegrityMismatch;
        await using var command = new NpgsqlCommand("""
            INSERT INTO synthetic_task_csv.export_audit VALUES(@event,@request,@ordinal,@run,@actor,@customer,@project,@environment,@utc,@stage,@outcome,@reason,@snapshot,@output,@rows,@contract,@digest)
            """, connection, transaction);
        foreach (var (name, value) in new (string, object)[] { ("event", audit.EventId), ("request", audit.RequestId), ("ordinal", stages.Count), ("run", audit.RunId), ("actor", audit.ActorId),
            ("customer", audit.Scope.CustomerId), ("project", audit.Scope.ProjectId), ("environment", audit.Scope.EnvironmentId), ("utc", audit.RecordedAtUtc),
            ("stage", audit.Stage.ToString()), ("outcome", audit.Stage == CsvAuditStage.Denial ? "Denied" : "Accepted"), ("reason", audit.Reason.ToString()),
            ("contract", CsvCodec.ContractVersion), ("digest", digest) }) command.Parameters.AddWithValue(name, value);
        command.Parameters.Add(new("snapshot", NpgsqlTypes.NpgsqlDbType.Text) { Value = (object?)audit.SnapshotDigest ?? DBNull.Value });
        command.Parameters.Add(new("output", NpgsqlTypes.NpgsqlDbType.Text) { Value = (object?)audit.OutputSha256 ?? DBNull.Value });
        command.Parameters.Add(new("rows", NpgsqlTypes.NpgsqlDbType.Integer) { Value = (object?)audit.RowCount ?? DBNull.Value });
        await command.ExecuteNonQueryAsync(cancellationToken);
        return null;
    }
    private bool Owns(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        var expected = new NpgsqlConnectionStringBuilder(connectionString); var actual = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
        return transaction.Connection == connection && connection.State == System.Data.ConnectionState.Open && expected.Host == actual.Host && expected.Port == actual.Port && expected.Database == actual.Database && expected.Username == actual.Username;
    }
    private static bool Valid(CsvAuditEvent audit) => audit is not null && audit.EventId != Guid.Empty && audit.RequestId != Guid.Empty && audit.RunId != Guid.Empty &&
        audit.Scope == CsvScope.Fixed && audit.ActorId is { Length: > 0 and <= 128 } && audit.ActorId.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-') &&
        audit.RecordedAtUtc.Offset == TimeSpan.Zero && audit.RecordedAtUtc >= DateTimeOffset.UnixEpoch && Enum.IsDefined(audit.Stage) && Enum.IsDefined(audit.Reason) &&
        (audit.Stage == CsvAuditStage.Denial ? audit.Reason != CsvAuditReason.None : audit.Reason == CsvAuditReason.None) &&
        (audit.SnapshotDigest is null || CsvCodec.ValidDigest(audit.SnapshotDigest)) && (audit.OutputSha256 is null || CsvCodec.ValidDigest(audit.OutputSha256)) &&
        (audit.RowCount is null || audit.RowCount is >= 0 and <= CsvCodec.MaximumRows) &&
        (audit.Stage is CsvAuditStage.Request or CsvAuditStage.Denial || audit.SnapshotDigest is not null && audit.RowCount is not null) &&
        (audit.Stage is not (CsvAuditStage.Render or CsvAuditStage.Delivery) || audit.OutputSha256 is not null) &&
        (audit.Reason != CsvAuditReason.TransferInterrupted || audit.Stage == CsvAuditStage.Denial && audit.SnapshotDigest is not null && audit.OutputSha256 is not null && audit.RowCount is not null);
}
