using System.Data;
using System.Text.Json;
using Npgsql;
using SyntheticAiValidation;

namespace SyntheticAiExecution;

public sealed partial class SyntheticAiExecutionStore
{
    public async Task<AiOperationResult<AiAcceptedSchemaProof>> ReadAcceptedSchemaInTransactionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        AiAuthority authority, Guid runId, string expectedSourceSnapshotDigest, CancellationToken cancellationToken = default)
    {
        if (AiExecutionPolicy.Authorize(authority, AiAction.Read) is { } denied) return Deny<AiAcceptedSchemaProof>(denied);
        if (runId == Guid.Empty || !AiExecutionPolicy.ValidDigest(expectedSourceSnapshotDigest) || !AcceptedSchemaActiveTransaction(connection, transaction))
            return Deny<AiAcceptedSchemaProof>(AiIssue.InvalidInput);
        AiOperationResult<AiExecutionSnapshot> read;
        try { read = await ReadInTransactionAsync(connection, transaction, authority, runId, cancellationToken); }
        catch (ArgumentException) { return Deny<AiAcceptedSchemaProof>(AiIssue.InvalidInput); }
        catch (PostgresException exception) when (exception.SqlState is PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.InvalidSchemaName)
        { return Deny<AiAcceptedSchemaProof>(AiIssue.NotInitialized); }
        if (read.Issue is { } issue) return Deny<AiAcceptedSchemaProof>(issue);
        var snapshot = read.Value;
        if (snapshot is null || snapshot.ContentDigest != expectedSourceSnapshotDigest || snapshot.RunLock.RunId != runId)
            return Deny<AiAcceptedSchemaProof>(AiIssue.SourceConflict);
        if (snapshot.Works.Any(w => w.State is not (AiWorkState.Succeeded or AiWorkState.Failed)))
            return Deny<AiAcceptedSchemaProof>(AiIssue.InvalidState);
        try
        {
            var works = new List<AiAcceptedSchemaWork>();
            foreach (var work in snapshot.Works)
            {
                using var input = JsonDocument.Parse(work.Work.PacketInputJson);
                var inputSchema = input.RootElement.GetProperty("schemaVersion").GetString() ?? throw new IntegrityException();
                var built = SyntheticAiPacketBuilder.Build(work.Work.PacketInputJson);
                if (!built.Succeeded || built.Packet is null) throw new IntegrityException();
                AiAcceptedSchemaRecord? accepted = null;
                if (work.State == AiWorkState.Succeeded)
                {
                    var attempt = work.Attempts[^1]; var receipt = attempt.Receipt ?? throw new IntegrityException();
                    if (receipt.Outcome != AiProviderOutcome.Response || receipt.Attempt != attempt.Key || built.Packet.ContentDigest != attempt.Key.PacketDigest || receipt.OutputDigest is null)
                        throw new IntegrityException();
                    await using var command = new NpgsqlCommand("SELECT canonical,digest,source_json FROM synthetic_ai_execution.accepted_snapshot WHERE attempt_id=@attempt", connection, transaction);
                    command.Parameters.AddWithValue("attempt", attempt.Key.AttemptId);
                    string canonical; string digest; string source;
                    await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                    {
                        if (!await reader.ReadAsync(cancellationToken) || reader.IsDBNull(0) || reader.IsDBNull(1) || reader.IsDBNull(2)) throw new IntegrityException();
                        canonical = reader.GetString(0); digest = reader.GetString(1); source = reader.GetString(2);
                    }
                    if (AiExecutionCanonical.Hash(canonical) != digest || AiExecutionCanonical.Hash(source) != receipt.OutputDigest) throw new IntegrityException();
                    using var output = JsonDocument.Parse(source); using var normalized = JsonDocument.Parse(canonical);
                    string Schema(JsonElement value)
                    {
                        if (value.GetProperty("runId").GetString() != runId.ToString("D") || value.GetProperty("packetDigest").GetString() != attempt.Key.PacketDigest)
                            throw new IntegrityException();
                        var schema = value.GetProperty("schemaVersion").GetString();
                        if (!AiExecutionPolicy.ValidText(schema, 256)) throw new IntegrityException();
                        return schema!;
                    }
                    accepted = new(attempt.Key, receipt.ReceiptId, receipt.OutputDigest, Schema(output.RootElement), digest, Schema(normalized.RootElement));
                }
                works.Add(new(work.Work.WorkId, work.Work.Category, work.Revision, work.State, inputSchema, built.Packet.ContentDigest, accepted, accepted is null ? "NoAcceptedOutput" : null));
            }
            return new(null, new(snapshot.RunLock, snapshot.ContentDigest, works));
        }
        catch (Exception exception) when (IntegrityFailure(exception) || exception is KeyNotFoundException)
        { return Deny<AiAcceptedSchemaProof>(AiIssue.IntegrityMismatch); }
        catch (PostgresException exception) when (exception.SqlState is PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.InvalidSchemaName)
        { return Deny<AiAcceptedSchemaProof>(AiIssue.NotInitialized); }
    }
    private static bool AcceptedSchemaActiveTransaction(NpgsqlConnection? connection, NpgsqlTransaction? transaction)
    {
        try
        {
            if (connection?.State != ConnectionState.Open || transaction is null || transaction.Connection != connection) return false;
            using var probe = new NpgsqlCommand { Connection = connection, Transaction = transaction };
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException) { return false; }
    }
}
