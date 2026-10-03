using Npgsql;

namespace IdentitySessions;

public sealed class PostgreSqlSecurityAudit
{
    public SecurityAuditBindingV1 Binding { get; }
    private readonly TimeProvider clock;
    public PostgreSqlSecurityAudit(SecurityAuditBindingV1 binding, TimeProvider clock)
    { SecurityAuditCanonical.Validate(binding); Binding = binding; this.clock = clock ?? throw new ArgumentNullException(nameof(clock)); }

    public async Task<CommittedSecurityAuditEventV1> AppendAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        SecurityAuditEventV1 value, CancellationToken cancellationToken = default)
    {
        SecurityAuditCanonical.Validate(value);
        long sequence; string previous;
        await using (var head = new NpgsqlCommand("SELECT sequence,digest FROM security_audit.lock_head($1,$2,$3)", connection, transaction))
        {
            head.Parameters.AddWithValue(Binding.StreamId); head.Parameters.AddWithValue(Binding.EnvironmentId); head.Parameters.AddWithValue(Binding.WriterBindingReference);
            await using var reader = await head.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Audit head unavailable.");
            sequence = checked(reader.GetInt64(0) + 1); previous = reader.GetString(1);
        }
        if (value.EventAtUtc > clock.GetUtcNow()) throw new InvalidOperationException("Future audit event refused.");
        // The ordered stream's trusted timestamp is sampled after its lock wait;
        // a slow competing operation cannot append a backdated admission event.
        value = value with { EventAtUtc = clock.GetUtcNow() };
        var canonical = SecurityAuditCanonical.Event(Binding, value, sequence, previous);
        var digest = SecurityAuditCanonical.Hash(canonical);
        await using var append = new NpgsqlCommand("SELECT security_audit.append_event($1,$2,$3,$4)", connection, transaction);
        append.Parameters.AddWithValue(Binding.StreamId); append.Parameters.AddWithValue(Binding.WriterBindingReference);
        append.Parameters.AddWithValue(canonical); append.Parameters.AddWithValue(digest);
        await append.ExecuteNonQueryAsync(cancellationToken);
        return new(value.EventId, Binding.StreamId, sequence, digest);
    }

    public async Task<OperationReceiptV1?> ResolveReceiptAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        OperationReceiptRequestV1 request, CancellationToken cancellationToken = default)
    {
        var canonical = SecurityAuditCanonical.Request(request);
        if (request.Issuer != Binding.Writer) throw new InvalidOperationException("Untrusted receipt issuer.");
        await using var query = new NpgsqlCommand("SELECT request_canonical,receipt_canonical FROM security_audit.read_receipt($1)", connection, transaction);
        query.Parameters.AddWithValue(request.OperationId);
        await using var reader = await query.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        if (reader.GetString(0) != canonical) throw new InvalidOperationException("Operation receipt binding conflict.");
        var receipt = SecurityAuditCanonical.ReadReceipt(reader.GetString(1));
        if (SecurityAuditCanonical.Request(receipt.Request) != canonical) throw new InvalidOperationException("Operation receipt integrity failure.");
        return receipt;
    }

    public async Task RecordReceiptAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        OperationReceiptV1 receipt, CancellationToken cancellationToken = default)
    {
        if (receipt.Request.Issuer != Binding.Writer || receipt.CommittedAtUtc > clock.GetUtcNow()) throw new InvalidOperationException("Untrusted receipt.");
        var canonicalRequest = SecurityAuditCanonical.Request(receipt.Request);
        var canonicalReceipt = SecurityAuditCanonical.Receipt(receipt);
        await using var append = new NpgsqlCommand("SELECT security_audit.append_receipt($1,$2,$3,$4)", connection, transaction);
        append.Parameters.AddWithValue(receipt.Request.OperationId); append.Parameters.AddWithValue(canonicalRequest);
        append.Parameters.AddWithValue(canonicalReceipt); append.Parameters.AddWithValue(receipt.EventIds.ToArray());
        await append.ExecuteNonQueryAsync(cancellationToken);
    }
}
