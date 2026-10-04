using IdentityAuthority;
using IdentitySessions;

namespace IgaMigration.BffFoundation;

// Opt-in projection of the approved internal authority. No registration, provider
// transport, enrollment or host activation occurs here.
public sealed class PostgreSqlBffSubjectAuthority(PostgreSqlIdentityAuthority authority, TimeProvider clock) : IBffSubjectAuthority
{
    public async ValueTask<SubjectAdmission?> CheckAsync(HumanSubject subject, CancellationToken cancellationToken)
    {
        var snapshot = await authority.ReadAsync(new SessionSubject(subject.TenantId, subject.ObjectId), cancellationToken);
        var now = clock.GetUtcNow();
        if (!authority.Eligible(snapshot, now) || snapshot!.Enrollment.Subject != new SessionSubject(subject.TenantId, subject.ObjectId)) return null;
        var external = snapshot.Enrollment.Origin == OrganizationalOrigin.ExternalOrganizational;
        // External Members have the same reviewed home/lifecycle boundary as Guests.
        var checkedAt = snapshot.Provider!.StartedAtUtc;
        if (external && snapshot.HomeStatusCheckedAtUtc is { } home && home < checkedAt) checkedAt = home;
        return new(subject, true, true, true, true, checkedAt, snapshot.SecurityVersion,
            authority.CoarseRoles(snapshot, now).Select(role => role.ToString()).ToArray(),
            PostgreSqlIdentityAuthority.EffectiveCutoff(snapshot), external, external,
            external ? snapshot.Enrollment.HomeTenantId : null);
    }
}
