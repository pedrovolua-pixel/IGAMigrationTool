using Npgsql;

namespace IdentitySessions;

internal sealed record SubjectState(bool Active, long Version, DateTimeOffset ProviderChecked);

internal static class SessionDatabase
{
    public static async Task<SubjectState?> LockSubjectAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, SessionSubject subject, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT active, security_version, provider_checked_at FROM identity_sessions.subjects
            WHERE tenant_id=$1 AND object_id=$2 FOR UPDATE
            """, connection, transaction);
        command.Parameters.AddWithValue(subject.TenantId);
        command.Parameters.AddWithValue(subject.ObjectId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new SubjectState(reader.GetBoolean(0), reader.GetInt64(1), reader.GetFieldValue<DateTime>(2))
            : null;
    }

    public static bool Current(SubjectState? state, DateTimeOffset now, long version) =>
        state is { Active: true } && state.Version == version && state.ProviderChecked <= now &&
        now - state.ProviderChecked < SessionTicket.ProviderFreshness;
}
