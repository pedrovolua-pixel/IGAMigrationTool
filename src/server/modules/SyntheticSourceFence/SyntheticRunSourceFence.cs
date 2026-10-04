using System.Buffers.Binary;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;

namespace SyntheticSourceFence;

/// <summary>Transaction-scoped synthetic source serialization; no domain reads or authority decisions.</summary>
public static class SyntheticRunSourceFence
{
    /// <summary>Ordered delivery exclusion spanning a committed audit and first output bytes. Caller must close a nonpooled connection after use.</summary>
    public static async Task AcquireSessionAsync(DbConnection connection, string customerId, string projectId, string environmentId,
        Guid runId, CancellationToken cancellationToken = default)
    {
        if (connection.State != System.Data.ConnectionState.Open || runId == Guid.Empty || string.IsNullOrWhiteSpace(customerId) ||
            string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(environmentId)) throw new ArgumentException("Invalid session source fence.");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = "synthetic-source-fence-v1", customerId, projectId, environmentId, runId = runId.ToString("D") });
        await using var command = connection.CreateCommand(); command.CommandTimeout = 15; command.CommandText = "SELECT pg_advisory_lock(@source_key)";
        var parameter = command.CreateParameter(); parameter.ParameterName = "source_key"; parameter.DbType = System.Data.DbType.Int64;
        parameter.Value = BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(bytes)); command.Parameters.Add(parameter);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
    public static async Task AcquireAsync(DbConnection connection, DbTransaction transaction,
        string customerId, string projectId, string environmentId, Guid runId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        if (transaction.Connection != connection || runId == Guid.Empty ||
            string.IsNullOrWhiteSpace(customerId) || string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(environmentId))
            throw new ArgumentException("Invalid synthetic source fence binding.");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = "synthetic-source-fence-v1",
            customerId,
            projectId,
            environmentId,
            runId = runId.ToString("D")
        });
        var key = BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(bytes));
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT pg_advisory_xact_lock(@source_key)";
        command.CommandTimeout = 15;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "source_key";
        parameter.DbType = System.Data.DbType.Int64;
        parameter.Value = key;
        command.Parameters.Add(parameter);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
