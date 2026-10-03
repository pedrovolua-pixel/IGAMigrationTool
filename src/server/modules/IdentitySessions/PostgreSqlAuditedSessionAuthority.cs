using Npgsql;

namespace IdentitySessions;

// Trusted internal commands only; no browser route or provider enrollment. The
// caller supplies a server-owned operation and already authorized actor binding.
public sealed class PostgreSqlAuditedSessionAuthority(NpgsqlDataSource dataSource, TimeProvider clock, PostgreSqlSecurityAudit audit)
{
    public static string ExactRevocationDigest(SessionSubject subject, Guid reference) =>
        SecurityAuditCanonical.Hash($"RevokeExact:{subject.TenantId:D}:{subject.ObjectId:D}:{reference:D}");
    public static string SubjectRevocationDigest(SessionSubject subject, bool disable) =>
        SecurityAuditCanonical.Hash($"RevokeSubject:{subject.TenantId:D}:{subject.ObjectId:D}:{disable}");
    public async Task<OperationReceiptV1> RevokeExactAsync(OperationReceiptRequestV1 request, Guid reference,
        CancellationToken cancellationToken = default)
    {
        if (request.OperationKind != SecurityAuditAction.SessionRevoked || request.OldSessionReference != reference || reference == Guid.Empty ||
            request.CommandSha256 != ExactRevocationDigest(request.Target, reference))
            throw new InvalidOperationException("Exact reviewed revocation required.");
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var state = await SessionDatabase.LockSubjectAsync(connection, transaction, request.Target, cancellationToken, true)
            ?? throw new InvalidOperationException("Target subject unavailable.");
        var prior = await audit.ResolveReceiptAsync(connection, transaction, request, cancellationToken);
        if (prior is not null) return prior;
        await using var revoke = new NpgsqlCommand("SELECT security_audit.revoke_reference($1,$2,$3,$4,$5)", connection, transaction);
        revoke.Parameters.AddWithValue(request.Target.TenantId); revoke.Parameters.AddWithValue(request.Target.ObjectId);
        revoke.Parameters.AddWithValue(reference); revoke.Parameters.AddWithValue(clock.GetUtcNow()); revoke.Parameters.AddWithValue(request.OperationId);
        if ((bool?)await revoke.ExecuteScalarAsync(cancellationToken) != true) throw new InvalidOperationException("Exact unrevoked target required.");
        var receipt = await Finish(connection, transaction, request, state.Version, reference, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return receipt;
    }

    public async Task<OperationReceiptV1> RevokeSubjectAsync(OperationReceiptRequestV1 request, bool disable,
        CancellationToken cancellationToken = default)
    {
        if (request.OperationKind != SecurityAuditAction.SubjectRevoked || request.OldSessionReference is not null ||
            request.CommandSha256 != SubjectRevocationDigest(request.Target, disable))
            throw new InvalidOperationException("Exact subject revocation required.");
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var state = await SessionDatabase.LockSubjectAsync(connection, transaction, request.Target, cancellationToken, true)
            ?? throw new InvalidOperationException("Target subject unavailable.");
        var prior = await audit.ResolveReceiptAsync(connection, transaction, request, cancellationToken);
        if (prior is not null) return prior;
        var version = checked(state.Version + 1);
        await using var revoke = new NpgsqlCommand("SELECT security_audit.revoke_subject($1,$2,$3,$4,$5)", connection, transaction);
        revoke.Parameters.AddWithValue(request.Target.TenantId); revoke.Parameters.AddWithValue(request.Target.ObjectId);
        revoke.Parameters.AddWithValue(disable); revoke.Parameters.AddWithValue(clock.GetUtcNow()); revoke.Parameters.AddWithValue(request.OperationId);
        var result = (long?)await revoke.ExecuteScalarAsync(cancellationToken);
        if (result != version) throw new InvalidOperationException("Subject security version conflict.");
        var receipt = await Finish(connection, transaction, request, version, null, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return receipt;
    }

    private async Task<OperationReceiptV1> Finish(NpgsqlConnection connection, NpgsqlTransaction transaction,
        OperationReceiptRequestV1 request, long version, Guid? reference, CancellationToken ct)
    {
        var evt = new SecurityAuditEventV1(Guid.NewGuid(), request.OperationId, clock.GetUtcNow(), request.ActorKind,
            request.Actor, Guid.NewGuid(), request.OperationKind, SecurityAuditOutcome.Succeeded, SecurityAuditReason.None,
            request.Target, reference, null, version);
        var committed = await audit.AppendAsync(connection, transaction, evt, ct);
        var receipt = new OperationReceiptV1(request, SecurityAuditOutcome.Succeeded, clock.GetUtcNow(), null, version, null, [committed.EventId]);
        await audit.RecordReceiptAsync(connection, transaction, receipt, ct);
        return receipt;
    }
}
