using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using IdentityPolicy;
using IdentitySessions;
using Npgsql;
using NpgsqlTypes;

namespace IdentityAuthority;

public sealed class PostgreSqlIdentityAuthority(NpgsqlDataSource dataSource, PostgreSqlSecurityAudit audit,
    ProviderRoleBindingV1 roleBinding, TimeProvider clock) : ITransactionalSessionAdmissionPolicy
{
    public async Task<AuthorityReceiptV1> ExecuteAsync(AuthorityCommandV1 command, CancellationToken cancellationToken = default)
    {
        AuthorityCodec.Validate(command); AuthorityCodec.Validate(roleBinding);
        var d = command.Decision;
        AuthorityCodec.Require(d.Subject.TenantId == roleBinding.ResourceTenantId && d.Administrator.TenantId == roleBinding.ResourceTenantId);
        var action = d.Operation == AuthorityOperation.RevokeSubject ? SecurityAuditAction.SubjectRevoked : SecurityAuditAction.AuthorityChanged;
        var scope = d.Scope;
        var request = new OperationReceiptRequestV1(d.CommandId, audit.Binding.Writer, SecurityAuditActorKind.Human,
            d.Administrator, action, d.Subject, d.PayloadSha256, scope?.CustomerId, scope?.ProjectId, scope?.EnvironmentId, scope?.AssessmentId);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        _ = await LockedSnapshot(connection, transaction, d.Subject, cancellationToken);
        var prior = await audit.ResolveReceiptAsync(connection, transaction, request, cancellationToken);
        if (prior is not null) return Receipt(prior);
        CheckCommandTime(command, clock.GetUtcNow());
        long revision; long version;
        await using (var apply = new NpgsqlCommand("SELECT revision,security_version FROM identity_authority.apply_command($1)", connection, transaction))
        {
            apply.Parameters.AddWithValue(NpgsqlDbType.Jsonb, AuthorityCodec.Serialize(command));
            await using var result = await apply.ExecuteReaderAsync(cancellationToken);
            if (!await result.ReadAsync(cancellationToken)) throw new InvalidOperationException("Authority mutation denied.");
            revision = result.GetInt64(0); version = result.GetInt64(1);
        }
        var eventTime = clock.GetUtcNow();
        var evt = new SecurityAuditEventV1(Guid.NewGuid(), d.CommandId, eventTime, SecurityAuditActorKind.Human,
            d.Administrator, Guid.NewGuid(), action, SecurityAuditOutcome.Succeeded, SecurityAuditReason.None,
            d.Subject, null, null, version, scope?.CustomerId, scope?.ProjectId);
        var written = await audit.AppendAsync(connection, transaction, evt, cancellationToken);
        var receipt = new OperationReceiptV1(request, SecurityAuditOutcome.Succeeded, clock.GetUtcNow(), revision, version, null, [written.EventId]);
        await audit.RecordReceiptAsync(connection, transaction, receipt, cancellationToken);
        CheckCommandTime(command, clock.GetUtcNow());
        await transaction.CommitAsync(cancellationToken);
        return Receipt(receipt);
    }

    public async Task<AuthorityReceiptV1> PublishAsync(ProviderReadResultV1 result, Guid operationId,
        CancellationToken cancellationToken = default)
    {
        var p = result.Observation;
        AuthorityCodec.Validate(p); AuthorityCodec.Validate(roleBinding);
        AuthorityCodec.Require(operationId != Guid.Empty && p.Subject.TenantId == roleBinding.ResourceTenantId
            && p.ClientId == roleBinding.ClientId && p.ResourceServicePrincipalId == roleBinding.ResourceServicePrincipalId
            && p.AppRoleIds.All(id => roleBinding.AppRoles.Any(r => r.AppRoleId == id && r.Enabled)));
        var digest = SecurityAuditCanonical.Hash(AuthorityCodec.Serialize(result));
        var request = new OperationReceiptRequestV1(operationId, audit.Binding.Writer, SecurityAuditActorKind.Workload,
            audit.Binding.Writer, SecurityAuditAction.AuthorityChanged, p.Subject, digest);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var snapshot = await LockedSnapshot(connection, transaction, p.Subject, cancellationToken);
        var prior = await audit.ResolveReceiptAsync(connection, transaction, request, cancellationToken);
        if (prior is not null) return Receipt(prior);
        AuthorityCodec.Require(snapshot is not null && snapshot.Enrollment.Revision == p.EnrollmentRevision
            && snapshot.SecurityVersion == p.SecurityVersion && snapshot.Enrollment.Lifecycle == EnrollmentLifecycle.Active);
        CheckProviderTime(result, snapshot!.Enrollment, clock.GetUtcNow());
        long revision; long version;
        await using (var publish = new NpgsqlCommand("SELECT revision,security_version FROM identity_authority.publish_provider($1,$2,$3,$4)", connection, transaction))
        {
            publish.Parameters.AddWithValue(NpgsqlDbType.Jsonb, AuthorityCodec.Serialize(p));
            publish.Parameters.AddWithValue(NpgsqlDbType.Jsonb, result.HomeStatus is null ? DBNull.Value : AuthorityCodec.Serialize(result.HomeStatus));
            publish.Parameters.AddWithValue(operationId); publish.Parameters.AddWithValue(digest);
            await using var reader = await publish.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Provider publication denied.");
            revision = reader.GetInt64(0); version = reader.GetInt64(1);
        }
        var evt = new SecurityAuditEventV1(Guid.NewGuid(), operationId, clock.GetUtcNow(), SecurityAuditActorKind.Workload,
            audit.Binding.Writer, p.CorrelationId, SecurityAuditAction.AuthorityChanged, SecurityAuditOutcome.Succeeded,
            SecurityAuditReason.None, p.Subject, null, null, version);
        var written = await audit.AppendAsync(connection, transaction, evt, cancellationToken);
        var receipt = new OperationReceiptV1(request, SecurityAuditOutcome.Succeeded, clock.GetUtcNow(), revision, version, null, [written.EventId]);
        await audit.RecordReceiptAsync(connection, transaction, receipt, cancellationToken);
        CheckProviderTime(result, snapshot.Enrollment, clock.GetUtcNow());
        await transaction.CommitAsync(cancellationToken);
        return Receipt(receipt);
    }
    public async ValueTask<AuthoritySnapshotV1?> ReadAsync(SessionSubject subject, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!AuthorityCodec.Subject(subject) || subject.TenantId != roleBinding.ResourceTenantId) return null;
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            return await LockedSnapshot(connection, transaction, subject, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return null; }
    }
    public async ValueTask<bool> IsEligibleAsync(SessionSubject subject, CancellationToken cancellationToken)
        => Eligible(await ReadAsync(subject, cancellationToken), clock.GetUtcNow());
    public async ValueTask<bool> IsEligibleAsync(SessionSubject subject, NpgsqlConnection connection,
        NpgsqlTransaction transaction, DateTimeOffset originalAuthenticatedUtc, CancellationToken cancellationToken)
    {
        var snapshot = await LockedSnapshot(connection, transaction, subject, cancellationToken);
        var now = clock.GetUtcNow();
        return Eligible(snapshot, now) && AuthorityCodec.Utc(originalAuthenticatedUtc) && originalAuthenticatedUtc <= now
            && EffectiveCutoff(snapshot!) is { } cutoff && originalAuthenticatedUtc >= cutoff;
    }
    public static DateTimeOffset? EffectiveCutoff(AuthoritySnapshotV1 snapshot)
    {
        if (snapshot.Provider is not { } provider) return null;
        if (snapshot.Enrollment.Origin == OrganizationalOrigin.ExternalOrganizational)
            return snapshot.HomeCutoffUtc is { } home ? (home > provider.ResourceCutoffUtc ? home : provider.ResourceCutoffUtc) : null;
        return provider.ResourceCutoffUtc;
    }
    public bool Eligible(AuthoritySnapshotV1? snapshot, DateTimeOffset now)
    {
        if (snapshot is null || snapshot.Enrollment.Lifecycle != EnrollmentLifecycle.Active || snapshot.SecurityVersion < 1
            || !snapshot.ProviderStatusValid || snapshot.Provider is not { } p || snapshot.ProviderPublishedSecurityVersion != snapshot.SecurityVersion
            || p.EnrollmentRevision != snapshot.Enrollment.Revision || !p.AccountEnabled || p.AppRoleIds.IsDefaultOrEmpty
            || !SyntheticProviderReader.Fresh(p.StartedAtUtc, now) || p.CompletedAtUtc > now || p.ResourceCutoffUtc > now
            || p.ClientId != roleBinding.ClientId || p.ResourceServicePrincipalId != roleBinding.ResourceServicePrincipalId
            || p.Subject != snapshot.Enrollment.Subject || p.Subject.TenantId != roleBinding.ResourceTenantId
            || p.AppRoleIds.Any(id => !roleBinding.AppRoles.Any(r => r.AppRoleId == id && r.Enabled))
            || snapshot.Assignments.IsDefaultOrEmpty || !snapshot.Assignments.Any(a => a.Active && a.StartsAtUtc <= now && a.ExpiresAtUtc > now)) return false;
        if (snapshot.Enrollment.Origin == OrganizationalOrigin.ExternalOrganizational)
        {
            var g = snapshot.Guest;
            if (g is null || !snapshot.SponsorActive || g.SponsorOrEngagementChanged || g.AssignedAtUtc > now || g.ExpiresAtUtc <= now
                || g.LastReviewedAtUtc > now || now - g.LastReviewedAtUtc > TimeSpan.FromDays(30) || p.InvitationState != InvitationState.Accepted
                || !snapshot.HomeStatusVerified || snapshot.HomeStatusCheckedAtUtc is not { } home || !SyntheticProviderReader.Fresh(home, now)
                || snapshot.HomeCutoffUtc is not { } cutoff || cutoff > now) return false;
        }
        else if (snapshot.Guest is not null || snapshot.Enrollment.HomeTenantId != roleBinding.ResourceTenantId) return false;
        return true;
    }
    public ImmutableArray<HumanAssignment> ExactAssignments(AuthoritySnapshotV1? snapshot, HumanScope scope, string category, DateTimeOffset now)
    {
        if (!Eligible(snapshot, now) || !AuthorityCodec.Scope(scope) || !AuthorityCodec.Key(category)) return [];
        return snapshot!.Assignments.Where(a => a.Active && a.Scope == scope && a.StartsAtUtc <= now && a.ExpiresAtUtc > now
                && a.EvidenceCategories.Contains(category, StringComparer.Ordinal))
            .Select(a => new HumanAssignment(a.Role, a.Scope, true, a.ExpiresAtUtc, a.EvidenceCategories.ToImmutableHashSet(StringComparer.Ordinal), a.Conditions.ToImmutableHashSet())).ToImmutableArray();
    }
    public ImmutableArray<CoarseAppRole> CoarseRoles(AuthoritySnapshotV1? snapshot, DateTimeOffset now) => !Eligible(snapshot, now) ? []
        : snapshot!.Provider!.AppRoleIds.Select(id => roleBinding.AppRoles.Single(r => r.AppRoleId == id && r.Enabled).Role).Order().ToImmutableArray();
    private async Task<AuthoritySnapshotV1?> LockedSnapshot(NpgsqlConnection connection, NpgsqlTransaction transaction,
        SessionSubject subject, CancellationToken cancellationToken)
    {
        await using var query = new NpgsqlCommand("SELECT identity_authority.lock_subject($1,$2)", connection, transaction);
        query.Parameters.AddWithValue(subject.TenantId); query.Parameters.AddWithValue(subject.ObjectId);
        var text = await query.ExecuteScalarAsync(cancellationToken) as string;
        if (text is null) return null;
        using var document = JsonDocument.Parse(text);
        var r = document.RootElement;
        T Read<T>(string field) => AuthorityCodec.Parse<T>(Encoding.UTF8.GetBytes(r.GetProperty(field).GetRawText()));
        var e = Read<SubjectEnrollmentV1>("enrollment");
        var assignments = r.GetProperty("assignments").EnumerateArray().Select(a => AuthorityCodec.Parse<HumanAssignmentV1>(Encoding.UTF8.GetBytes(a.GetRawText()))).ToImmutableArray();
        AuthorityCodec.Require(e.Subject == subject && assignments.All(a => a.Subject == subject)
            && assignments.Select(a => (a.Role, a.Scope)).Distinct().Count() == assignments.Length);
        var guest = r.GetProperty("guest").ValueKind == JsonValueKind.Null ? null : Read<GuestLifecycleV1>("guest");
        var provider = r.GetProperty("provider").ValueKind == JsonValueKind.Null ? null : Read<ProviderObservationV1>("provider");
        var version = r.GetProperty("securityVersion").GetInt64();
        HomeStatusEvidenceV1? home = null;
        if (r.GetProperty("home").ValueKind != JsonValueKind.Null)
            home = AuthorityCodec.Parse<HomeStatusEvidenceV1>(Encoding.UTF8.GetBytes(r.GetProperty("home").GetRawText()));
        return new(e, version, assignments, guest, provider, r.GetProperty("providerStatusValid").GetBoolean(),
            r.GetProperty("providerPublishedSecurityVersion").ValueKind == JsonValueKind.Null ? null : r.GetProperty("providerPublishedSecurityVersion").GetInt64(),
            r.GetProperty("sponsorActive").GetBoolean(), provider is not null && SyntheticProviderReader.ValidHome(home, e, provider.SecurityVersion, clock.GetUtcNow()),
            home?.CheckedAtUtc, home?.CutoffUtc);
    }
    private static void CheckCommandTime(AuthorityCommandV1 c, DateTimeOffset now)
    {
        if (c.Enrollment is { } e) AuthorityCodec.Require(e.EnrolledAtUtc <= now && e.Attribution.ApprovedAtUtc <= now);
        if (c.Assignment is { } a) AuthorityCodec.Require(a.Attribution.ApprovedAtUtc <= now);
        if (c.Guest is { } g) AuthorityCodec.Require(g.Attribution.ApprovedAtUtc <= now && g.AssignedAtUtc <= now
            && g.LastReviewedAtUtc <= now && now - g.LastReviewedAtUtc <= TimeSpan.FromDays(30) && g.ExpiresAtUtc > now);
    }
    private static void CheckProviderTime(ProviderReadResultV1 r, SubjectEnrollmentV1 e, DateTimeOffset now)
    {
        AuthorityCodec.Require(SyntheticProviderReader.Fresh(r.Observation.StartedAtUtc, now) && r.Observation.CompletedAtUtc <= now
            && r.Observation.ResourceCutoffUtc <= now);
        if (e.Origin == OrganizationalOrigin.ExternalOrganizational)
            AuthorityCodec.Require(SyntheticProviderReader.ValidHome(r.HomeStatus, e, r.Observation.SecurityVersion, now));
        else AuthorityCodec.Require(r.HomeStatus is null);
    }
    private static AuthorityReceiptV1 Receipt(OperationReceiptV1 r) => new(r.Request.OperationId,
        r.ResultingRevision ?? throw new InvalidOperationException("Missing authority revision."), r.SecurityVersion, r.CommittedAtUtc, r.EventIds.ToImmutableArray());
}
