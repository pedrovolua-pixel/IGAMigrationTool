using Npgsql;

namespace IdentitySessions;

// Audited issuance/retrieval must use the already locked transaction. Opening a
// second subject-locking connection would deadlock against the ticket store.
public interface ITransactionalSessionAdmissionPolicy : ISessionAdmissionPolicy
{
    ValueTask<bool> IsEligibleAsync(SessionSubject subject, NpgsqlConnection connection,
        NpgsqlTransaction transaction, DateTimeOffset originalAuthenticatedUtc, CancellationToken cancellationToken);
}
