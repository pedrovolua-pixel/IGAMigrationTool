using Npgsql;

namespace IdentitySessions;

public sealed record PendingAuthenticationChallenge(string Reference, DateTimeOffset IssuedUtc);

// Caller carries this reference in framework-protected OIDC state. Consumption
// is attempted only after the complete supported protocol handler succeeds.
public interface IPendingAuthenticationChallengeStore
{
    ValueTask<PendingAuthenticationChallenge?> CreateAsync(DateTimeOffset now, CancellationToken cancellationToken);
    ValueTask<bool> TryConsumeAsync(PendingAuthenticationChallenge challenge, DateTimeOffset now, CancellationToken cancellationToken);
}

public sealed class DenyingAuthenticationChallengeStore : IPendingAuthenticationChallengeStore
{
    public ValueTask<PendingAuthenticationChallenge?> CreateAsync(DateTimeOffset now, CancellationToken cancellationToken)
        => ValueTask.FromResult<PendingAuthenticationChallenge?>(null);
    public ValueTask<bool> TryConsumeAsync(PendingAuthenticationChallenge challenge, DateTimeOffset now, CancellationToken cancellationToken)
        => ValueTask.FromResult(false);
}

public sealed class PostgreSqlAuthenticationChallengeStore(NpgsqlDataSource dataSource, TimeProvider? clock = null) : IPendingAuthenticationChallengeStore
{
    public async ValueTask<PendingAuthenticationChallenge?> CreateAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var reference = SessionTicket.NewKey();
        // PostgreSQL timestamptz precision is microseconds; preserve its exact
        // stored precision in the state rather than compare rounded timestamps.
        var issued = new DateTimeOffset(now.UtcTicks - now.UtcTicks % 10, TimeSpan.Zero);
        await using var command = dataSource.CreateCommand("""
            INSERT INTO identity_sessions.pending_challenges (key_hash,issued_at) VALUES ($1,$2)
            """);
        command.Parameters.AddWithValue(SessionTicket.HashKey(reference)!);
        command.Parameters.AddWithValue(issued);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return new(reference, issued);
    }

    public async ValueTask<bool> TryConsumeAsync(PendingAuthenticationChallenge challenge, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var hash = SessionTicket.HashKey(challenge.Reference);
        if (hash is null || challenge.IssuedUtc > now || now - challenge.IssuedUtc >= TimeSpan.FromMinutes(15))
        {
            return false;
        }
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var find = new NpgsqlCommand("SELECT issued_at,consumed_at FROM identity_sessions.pending_challenges WHERE key_hash=$1 FOR UPDATE", connection, transaction))
        {
            find.Parameters.AddWithValue(hash);
            await using var row = await find.ExecuteReaderAsync(cancellationToken);
            if (!await row.ReadAsync(cancellationToken) || !row.IsDBNull(1) || row.GetFieldValue<DateTime>(0) != challenge.IssuedUtc.UtcDateTime) return false;
        }
        // Acquiring a contended row may take longer than the authentication
        // lifetime. Caller time before that wait is insufficient evidence.
        now = (clock ?? TimeProvider.System).GetUtcNow();
        if (challenge.IssuedUtc > now || now - challenge.IssuedUtc >= TimeSpan.FromMinutes(15)) return false;
        await using var command = new NpgsqlCommand("""
            UPDATE identity_sessions.pending_challenges SET consumed_at=$3
            WHERE key_hash=$1 AND issued_at=$2 AND consumed_at IS NULL
            AND issued_at <= $3 AND $3 < issued_at + interval '15 minutes'
            """, connection, transaction);
        command.Parameters.AddWithValue(hash);
        command.Parameters.AddWithValue(challenge.IssuedUtc);
        command.Parameters.AddWithValue(now);
        var changed = await command.ExecuteNonQueryAsync(cancellationToken);
        var commitTime = (clock ?? TimeProvider.System).GetUtcNow();
        if (changed != 1 || commitTime < challenge.IssuedUtc || commitTime - challenge.IssuedUtc >= TimeSpan.FromMinutes(15)) return false;
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
