using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Npgsql;

namespace IdentitySessions;

public sealed class PostgreSqlTicketStore : ITicketStore
{
    private readonly NpgsqlDataSource dataSource;
    private readonly IDataProtector protector;
    private readonly TimeProvider clock;
    private readonly ISessionAdmissionPolicy admission;

    public PostgreSqlTicketStore(NpgsqlDataSource dataSource, IDataProtectionProvider protection,
        TimeProvider clock, ISessionAdmissionPolicy admission)
    {
        this.dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        protector = (protection ?? throw new ArgumentNullException(nameof(protection)))
            .CreateProtector("IGAMigrationTool.IdentitySessions.Ticket.v1");
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.admission = admission ?? throw new ArgumentNullException(nameof(admission));
    }

    public Task<string> StoreAsync(AuthenticationTicket ticket) => StoreAsync(ticket, CancellationToken.None);
    public async Task<string> StoreAsync(AuthenticationTicket ticket, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var context = SessionTicket.Validate(ticket, now);
        RequireIssuance(ticket, context.Authenticated, now);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var state = await SessionDatabase.LockSubjectAsync(connection, transaction, context.Subject, cancellationToken);
        if (!SessionDatabase.Current(state, now, context.Version) ||
            !await admission.IsEligibleAsync(context.Subject, cancellationToken))
        {
            throw new InvalidOperationException("Current authoritative session admission is required.");
        }
        now = clock.GetUtcNow();
        context = SessionTicket.Validate(ticket, now);
        RequireIssuance(ticket, context.Authenticated, now);
        if (!SessionDatabase.Current(state, now, context.Version))
        {
            throw new InvalidOperationException("Authoritative provider status expired during admission.");
        }
        var key = await InsertAsync(connection, transaction, ticket, context.Subject, state!, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return key;
    }

    public Task<AuthenticationTicket?> RetrieveAsync(string key) => RetrieveAsync(key, CancellationToken.None);
    public async Task<AuthenticationTicket?> RetrieveAsync(string key, CancellationToken cancellationToken)
    {
        var hash = SessionTicket.HashKey(key);
        if (hash is null)
        {
            return null;
        }
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var session = await LoadAsync(connection, transaction, hash, cancellationToken);
        var now = clock.GetUtcNow();
        if (!Valid(session, now) || !await admission.IsEligibleAsync(session!.Subject, cancellationToken))
        {
            return null;
        }
        now = clock.GetUtcNow();
        if (!Valid(session, now))
        {
            return null;
        }
        AuthenticationTicket? ticket;
        try
        {
            ticket = TicketSerializer.Default.Deserialize(protector.Unprotect(session!.Payload));
            if (ticket is null)
            {
                return null;
            }
            // Context properties must be well formed, but provider freshness is
            // read from the authoritative DB, not the protected issuance ticket.
            ticket.Properties.Items[SessionTicket.ProviderCheckedUtc] = session.State.ProviderChecked.ToString("O");
            var context = SessionTicket.Validate(ticket, now);
            if (context.Subject != session.Subject || context.Version != session.Version)
            {
                return null;
            }
            ticket = SessionTicket.Minimal(ticket, session.AbsoluteExpires, now, session.State.ProviderChecked);
            ticket.Properties.Items[SessionTicket.SessionReference] = session.Reference.ToString("D");
        }
        catch (Exception exception) when (exception is CryptographicException or InvalidOperationException)
        {
            return null;
        }
        await TouchAsync(connection, transaction, hash, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ticket;
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket) => RenewAsync(key, ticket, CancellationToken.None);
    public async Task RenewAsync(string key, AuthenticationTicket ticket, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var context = SessionTicket.Validate(ticket, now);
        var hash = SessionTicket.HashKey(key);
        if (hash is null)
        {
            return;
        }
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var session = await LoadAsync(connection, transaction, hash, cancellationToken);
        if (!Valid(session, now) || context.Subject != session!.Subject || context.Version != session.Version ||
            !await admission.IsEligibleAsync(context.Subject, cancellationToken))
        {
            return;
        }
        now = clock.GetUtcNow();
        if (!Valid(session, now))
        {
            return;
        }
        var original = TicketSerializer.Default.Deserialize(protector.Unprotect(session.Payload))
            ?? throw new InvalidOperationException("Stored session is invalid.");
        // Ordinary cookie sliding must never renew authentication age or change
        // privilege. Those changes require explicit rotation and new admission.
        if (original.Properties.Items[SessionTicket.AuthenticatedUtc] != ticket.Properties.Items[SessionTicket.AuthenticatedUtc] ||
            SessionTicket.Property(ticket, SessionTicket.SessionReference) != session.Reference.ToString("D") ||
            original.Properties.Items[SessionTicket.MfaCaVerified] != ticket.Properties.Items[SessionTicket.MfaCaVerified] ||
            !original.Principal.Claims.Select(c => (c.Type, c.Value)).Order().SequenceEqual(
                ticket.Principal.Claims.Select(c => (c.Type, c.Value)).Order()))
        {
            throw new InvalidOperationException("Authentication or privilege changes require session rotation.");
        }
        await TouchAsync(connection, transaction, hash, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public Task RemoveAsync(string key) => RemoveAsync(key, CancellationToken.None);
    public async Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        var hash = SessionTicket.HashKey(key);
        if (hash is null)
        {
            return;
        }
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var session = await LoadAsync(connection, transaction, hash, cancellationToken);
        if (session is null)
        {
            return;
        }
        await RevokeAsync(connection, transaction, hash, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<string?> RotateAsync(string oldKey, AuthenticationTicket replacement,
        CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var context = SessionTicket.Validate(replacement, now);
        RequireIssuance(replacement, context.Authenticated, now);
        var hash = SessionTicket.HashKey(oldKey);
        if (hash is null)
        {
            return null;
        }
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var session = await LoadAsync(connection, transaction, hash, cancellationToken);
        if (session is null || session.Revoked || session.Subject != context.Subject ||
            !SessionDatabase.Current(session.State, now, context.Version) ||
            !await admission.IsEligibleAsync(context.Subject, cancellationToken))
        {
            return null;
        }
        now = clock.GetUtcNow();
        context = SessionTicket.Validate(replacement, now);
        RequireIssuance(replacement, context.Authenticated, now);
        if (!SessionDatabase.Current(session.State, now, context.Version))
        {
            return null;
        }
        var previousExpiry = new[] { session.LastSeen + SessionTicket.IdleLifetime, session.AbsoluteExpires }.Min();
        if (now >= previousExpiry && context.Authenticated < previousExpiry)
        {
            return null;
        }
        // A fresh trusted reauthentication may replace an idle/absolute-expired
        // session, but cannot replace a revoked key or bypass subject revocation.
        await RevokeAsync(connection, transaction, hash, cancellationToken);
        var key = await InsertAsync(connection, transaction, replacement, context.Subject,
            session.State, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return key;
    }

    private async Task<string> InsertAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        AuthenticationTicket ticket, SessionSubject subject, SubjectState state, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var key = SessionTicket.NewKey();
        var reference = Guid.NewGuid();
        var authenticated = SessionTicket.Validate(ticket, now).Authenticated;
        var absoluteExpires = authenticated + SessionTicket.AbsoluteLifetime;
        var minimal = SessionTicket.Minimal(ticket, absoluteExpires, now, state.ProviderChecked);
        minimal.Properties.Items[SessionTicket.SessionReference] = reference.ToString("D");
        var payload = protector.Protect(TicketSerializer.Default.Serialize(minimal));
        await using var command = new NpgsqlCommand("""
            INSERT INTO identity_sessions.tickets (key_hash,session_reference,tenant_id,object_id,
                security_version,created_at,last_seen_at,absolute_expires_at,protected_ticket)
            VALUES ($1,$2,$3,$4,$5,$6,$6,$7,$8)
            """, connection, transaction);
        command.Parameters.AddWithValue(SessionTicket.HashKey(key)!);
        command.Parameters.AddWithValue(reference);
        command.Parameters.AddWithValue(subject.TenantId);
        command.Parameters.AddWithValue(subject.ObjectId);
        command.Parameters.AddWithValue(state.Version);
        command.Parameters.AddWithValue(now);
        command.Parameters.AddWithValue(absoluteExpires);
        command.Parameters.AddWithValue(payload);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return key;
    }

    private sealed record StoredSession(SessionSubject Subject, SubjectState State, long Version, Guid Reference,
        DateTimeOffset Created, DateTimeOffset LastSeen, DateTimeOffset AbsoluteExpires, bool Revoked, byte[] Payload);

    private static bool Valid(StoredSession? session, DateTimeOffset now) => session is not null &&
        !session.Revoked && now >= session.LastSeen && now < session.AbsoluteExpires &&
        now - session.LastSeen < SessionTicket.IdleLifetime && SessionDatabase.Current(session.State, now, session.Version);

    private static async Task<StoredSession?> LoadAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, string hash, CancellationToken cancellationToken)
    {
        SessionSubject subject;
        await using (var find = new NpgsqlCommand("SELECT tenant_id,object_id FROM identity_sessions.tickets WHERE key_hash=$1", connection, transaction))
        {
            find.Parameters.AddWithValue(hash);
            await using var reader = await find.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }
            subject = new SessionSubject(reader.GetGuid(0), reader.GetGuid(1));
        }
        var state = await SessionDatabase.LockSubjectAsync(connection, transaction, subject, cancellationToken);
        if (state is null)
        {
            return null;
        }
        await using var command = new NpgsqlCommand("""
            SELECT security_version,created_at,last_seen_at,absolute_expires_at,revoked_at IS NOT NULL,protected_ticket,session_reference
            FROM identity_sessions.tickets WHERE key_hash=$1 FOR UPDATE
            """, connection, transaction);
        command.Parameters.AddWithValue(hash);
        await using var session = await command.ExecuteReaderAsync(cancellationToken);
        return await session.ReadAsync(cancellationToken)
            ? new StoredSession(subject, state, session.GetInt64(0), session.GetGuid(6), session.GetFieldValue<DateTime>(1),
                session.GetFieldValue<DateTime>(2), session.GetFieldValue<DateTime>(3), session.GetBoolean(4),
                session.GetFieldValue<byte[]>(5)) : null;
    }

    private static void RequireIssuance(AuthenticationTicket ticket, DateTimeOffset authenticated, DateTimeOffset now)
    {
        if (ticket.Properties.Items.ContainsKey(SessionTicket.SessionReference) ||
            now - authenticated >= SessionTicket.AbsoluteLifetime)
        {
            throw new InvalidOperationException("Unexpired trusted authentication is required for a new session identifier.");
        }
    }

    private static async Task TouchAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string hash, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("UPDATE identity_sessions.tickets SET last_seen_at=$2 WHERE key_hash=$1 AND revoked_at IS NULL", connection, transaction);
        command.Parameters.AddWithValue(hash);
        command.Parameters.AddWithValue(now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task RevokeAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string hash, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("UPDATE identity_sessions.tickets SET revoked_at=$2 WHERE key_hash=$1 AND revoked_at IS NULL", connection, transaction);
        command.Parameters.AddWithValue(hash);
        command.Parameters.AddWithValue(clock.GetUtcNow());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
