using Npgsql;

namespace IdentitySessions;

// Trusted server administration only. No request/controller is exposed and
// these operations never infer onboarding or authority from an Entra claim.
public sealed class PostgreSqlSessionAuthority(NpgsqlDataSource dataSource, TimeProvider clock)
{
    public async Task ProvisionAsync(SessionSubject subject, bool active,
        DateTimeOffset verifiedProviderUtc, CancellationToken cancellationToken = default)
    {
        ValidateSubject(subject);
        ValidateProviderTime(verifiedProviderUtc);
        await using var command = dataSource.CreateCommand("""
            INSERT INTO identity_sessions.subjects
            (tenant_id,object_id,active,security_version,provider_checked_at) VALUES ($1,$2,$3,1,$4)
            """);
        command.Parameters.AddWithValue(subject.TenantId);
        command.Parameters.AddWithValue(subject.ObjectId);
        command.Parameters.AddWithValue(active);
        command.Parameters.AddWithValue(verifiedProviderUtc);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<long> RevokeSubjectAsync(SessionSubject subject, bool disableSubject = false,
        CancellationToken cancellationToken = default)
    {
        ValidateSubject(subject);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var state = await SessionDatabase.LockSubjectAsync(connection, transaction, subject, cancellationToken)
            ?? throw new InvalidOperationException("Subject is not provisioned.");
        var nextVersion = checked(state.Version + 1);
        await using var command = new NpgsqlCommand("""
            UPDATE identity_sessions.subjects SET security_version=$3, active=$4 WHERE tenant_id=$1 AND object_id=$2
            """, connection, transaction);
        command.Parameters.AddWithValue(subject.TenantId);
        command.Parameters.AddWithValue(subject.ObjectId);
        command.Parameters.AddWithValue(nextVersion);
        command.Parameters.AddWithValue(disableSubject ? false : state.Active);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await using var revoke = new NpgsqlCommand("""
            UPDATE identity_sessions.tickets SET revoked_at=$3 WHERE tenant_id=$1 AND object_id=$2 AND revoked_at IS NULL
            """, connection, transaction);
        revoke.Parameters.AddWithValue(subject.TenantId);
        revoke.Parameters.AddWithValue(subject.ObjectId);
        revoke.Parameters.AddWithValue(clock.GetUtcNow());
        await revoke.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return nextVersion;
    }

    public async Task ConfirmProviderAsync(SessionSubject subject, DateTimeOffset verifiedProviderUtc,
        CancellationToken cancellationToken = default)
    {
        ValidateSubject(subject);
        ValidateProviderTime(verifiedProviderUtc);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        _ = await SessionDatabase.LockSubjectAsync(connection, transaction, subject, cancellationToken)
            ?? throw new InvalidOperationException("Subject is not provisioned.");
        await using var command = new NpgsqlCommand("""
            UPDATE identity_sessions.subjects SET provider_checked_at=$3
            WHERE tenant_id=$1 AND object_id=$2 AND provider_checked_at <= $3
            """, connection, transaction);
        command.Parameters.AddWithValue(subject.TenantId);
        command.Parameters.AddWithValue(subject.ObjectId);
        command.Parameters.AddWithValue(verifiedProviderUtc);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SessionDescriptor>> ListSessionsAsync(SessionSubject subject,
        CancellationToken cancellationToken = default)
    {
        ValidateSubject(subject);
        await using var command = dataSource.CreateCommand("""
            SELECT session_reference,created_at,last_seen_at,absolute_expires_at,revoked_at IS NOT NULL
            FROM identity_sessions.tickets WHERE tenant_id=$1 AND object_id=$2 ORDER BY created_at,session_reference
            """);
        command.Parameters.AddWithValue(subject.TenantId);
        command.Parameters.AddWithValue(subject.ObjectId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var sessions = new List<SessionDescriptor>();
        while (await reader.ReadAsync(cancellationToken))
        {
            sessions.Add(new SessionDescriptor(reader.GetGuid(0), reader.GetFieldValue<DateTime>(1),
                reader.GetFieldValue<DateTime>(2), reader.GetFieldValue<DateTime>(3), reader.GetBoolean(4)));
        }
        return sessions;
    }

    public async Task<bool> RevokeSessionAsync(SessionSubject subject, Guid reference,
        CancellationToken cancellationToken = default)
    {
        ValidateSubject(subject);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        if (await SessionDatabase.LockSubjectAsync(connection, transaction, subject, cancellationToken) is null)
        {
            return false;
        }
        await using var command = new NpgsqlCommand("""
            UPDATE identity_sessions.tickets SET revoked_at=$4 WHERE tenant_id=$1 AND object_id=$2
            AND session_reference=$3 AND revoked_at IS NULL
            """, connection, transaction);
        command.Parameters.AddWithValue(subject.TenantId);
        command.Parameters.AddWithValue(subject.ObjectId);
        command.Parameters.AddWithValue(reference);
        command.Parameters.AddWithValue(clock.GetUtcNow());
        var changed = await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return changed == 1;
    }

    private void ValidateProviderTime(DateTimeOffset value)
    {
        if (value > clock.GetUtcNow() || clock.GetUtcNow() - value >= SessionTicket.ProviderFreshness)
        {
            throw new ArgumentException("Provider verification must be current and cannot be future dated.");
        }
    }

    private static void ValidateSubject(SessionSubject subject)
    {
        if (subject.TenantId == Guid.Empty || subject.ObjectId == Guid.Empty)
        {
            throw new ArgumentException("Immutable tenant and subject are required.");
        }
    }
}
