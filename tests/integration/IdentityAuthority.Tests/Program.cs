using System.Collections.Immutable;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using IdentityAuthority;
using IdentityPolicy;
using IdentitySessions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Npgsql;
using NpgsqlTypes;

if (args is not ["--reset-synthetic-schema"]) throw new InvalidOperationException("Explicit isolated synthetic reset required.");
var connectionString = Environment.GetEnvironmentVariable("IGA_AUTHORITY_TEST_CONNECTION") ?? throw new InvalidOperationException("Synthetic database required.");
var settings = new NpgsqlConnectionStringBuilder(connectionString);
if (settings.Host is not ("localhost" or "127.0.0.1") || !settings.Database!.StartsWith("iga_synthetic_", StringComparison.Ordinal)) throw new InvalidOperationException("Loopback synthetic database only.");
await using var operatorDb = NpgsqlDataSource.Create(connectionString);
var checks = 0;
void Check(bool result, string label) { if (!result) throw new Exception(label); checks++; }
async Task Denied(Func<Task> action, string label)
{ try { await action(); throw new Exception("Expected denial: " + label); } catch (Exception e) when (e is InvalidOperationException or NpgsqlException) { checks++; } }
async Task Sql(string text) { await using var q = operatorDb.CreateCommand(text); await q.ExecuteNonQueryAsync(); }
await Sql("DROP SCHEMA IF EXISTS identity_authority CASCADE; DROP SCHEMA IF EXISTS security_audit CASCADE; DROP SCHEMA IF EXISTS identity_sessions CASCADE");
foreach (var name in new[] { "001-initial.sql", "002-authentication-context.sql", "003-atomic-audit.sql" })
{
    using var resource = typeof(PostgreSqlTicketStore).Assembly.GetManifestResourceStream("IdentitySessions." + name)!;
    using var reader = new StreamReader(resource); await Sql(await reader.ReadToEndAsync());
}
using (var resource = typeof(PostgreSqlIdentityAuthority).Assembly.GetManifestResourceStream("IdentityAuthority.001-authority.sql")!)
using (var reader = new StreamReader(resource)) await Sql(await reader.ReadToEndAsync());
var suffix = Guid.NewGuid().ToString("N")[..10];
var ownerName = "iga_auth_owner_" + suffix;
var adminName = "iga_auth_admin_" + suffix;
var providerName = "iga_auth_provider_" + suffix;
var readerName = "iga_auth_reader_" + suffix;
var runtimeName = "iga_auth_runtime_" + suffix;
await Sql($"CREATE ROLE {ownerName} NOLOGIN; CREATE ROLE {adminName} LOGIN; CREATE ROLE {providerName} LOGIN; CREATE ROLE {readerName} LOGIN; CREATE ROLE {runtimeName} LOGIN");
await Sql($"GRANT USAGE ON SCHEMA identity_authority,identity_sessions,security_audit TO {ownerName}; GRANT ALL ON ALL TABLES IN SCHEMA identity_authority TO {ownerName}; GRANT SELECT,INSERT,UPDATE ON ALL TABLES IN SCHEMA identity_sessions TO {ownerName}; GRANT SELECT ON security_audit.operation_receipts,security_audit.events TO {ownerName}");
await using (var funcs = operatorDb.CreateCommand("SELECT oid::regprocedure::text FROM pg_proc WHERE pronamespace='identity_authority'::regnamespace"))
{
    await using var rows = await funcs.ExecuteReaderAsync(); var names = new List<string>();
    while (await rows.ReadAsync()) names.Add(rows.GetString(0)); await rows.DisposeAsync();
    foreach (var function in names) await Sql($"ALTER FUNCTION {function} OWNER TO {ownerName}");
}
await Sql($"GRANT USAGE ON SCHEMA identity_authority,identity_sessions,security_audit TO {adminName},{providerName},{readerName},{runtimeName}; GRANT EXECUTE ON FUNCTION identity_authority.lock_subject(uuid,uuid) TO {adminName},{providerName},{readerName},{runtimeName}; GRANT EXECUTE ON FUNCTION identity_authority.apply_command(jsonb) TO {adminName}; GRANT EXECUTE ON FUNCTION identity_authority.publish_provider(jsonb,jsonb,uuid,text) TO {providerName}");
await Sql($"GRANT EXECUTE ON FUNCTION security_audit.lock_head(uuid,text,uuid),security_audit.append_event(uuid,uuid,text,text),security_audit.read_receipt(uuid),security_audit.append_receipt(uuid,text,text,uuid[]) TO {adminName},{providerName},{runtimeName}");
await Sql($"GRANT EXECUTE ON FUNCTION security_audit.issue_ticket(text,uuid,uuid,uuid,bigint,timestamptz,timestamptz,bytea,uuid),security_audit.revoke_ticket(text,timestamptz,uuid),security_audit.lock_subject(uuid,uuid),security_audit.lookup_ticket_subject(text),security_audit.lock_ticket(text),security_audit.touch_ticket(text,timestamptz) TO {runtimeName}");
var tenant = Guid.NewGuid(); var administrator = new SessionSubject(tenant, Guid.NewGuid()); var writer = new SessionSubject(tenant, Guid.NewGuid());
foreach (var (login, kind, identity) in new[] { (adminName, "Administrator", administrator), (providerName, "Provider", writer), (readerName, "Reader", writer), (runtimeName, "Reader", writer) })
{
    await using var q = operatorDb.CreateCommand("INSERT INTO identity_authority.writer_bindings VALUES($1,$2,$3,$4)");
    q.Parameters.AddWithValue(login); q.Parameters.AddWithValue(kind); q.Parameters.AddWithValue(identity.TenantId); q.Parameters.AddWithValue(identity.ObjectId); await q.ExecuteNonQueryAsync();
}
var auditBinding = new SecurityAuditBindingV1(Guid.NewGuid(), "synthetic-authority", Guid.NewGuid(), writer, Guid.NewGuid());
await using (var q = operatorDb.CreateCommand("INSERT INTO security_audit.streams(stream_id,environment_id,writer_binding_reference,writer_tenant_id,writer_object_id,application_client_id) VALUES($1,$2,$3,$4,$5,$6)"))
{
    q.Parameters.AddWithValue(auditBinding.StreamId); q.Parameters.AddWithValue(auditBinding.EnvironmentId); q.Parameters.AddWithValue(auditBinding.WriterBindingReference);
    q.Parameters.AddWithValue(writer.TenantId); q.Parameters.AddWithValue(writer.ObjectId); q.Parameters.AddWithValue(auditBinding.ApplicationClientId); await q.ExecuteNonQueryAsync();
}
foreach (var login in new[] { adminName, providerName, runtimeName })
{
    await using var q = operatorDb.CreateCommand("INSERT INTO security_audit.writer_roles VALUES($1,$2,$3,$4)");
    q.Parameters.AddWithValue(auditBinding.StreamId); q.Parameters.AddWithValue(login); q.Parameters.AddWithValue(auditBinding.WriterBindingReference);
    q.Parameters.AddWithValue(login == runtimeName ? new[] { "SessionIssued", "SessionRevoked", "SessionRotated" }
        : login == adminName ? new[] { "AuthorityChanged", "SubjectRevoked" } : new[] { "AuthorityChanged" });
    await q.ExecuteNonQueryAsync();
}
var roleBinding = new ProviderRoleBindingV1("provider-role-binding-v1", tenant, Guid.NewGuid(), Guid.NewGuid(),
    Enum.GetValues<CoarseAppRole>().Select(r => new ProviderAppRoleV1(Guid.NewGuid(), r, true)).ToImmutableArray(), 1, new string('a', 64), Guid.NewGuid());
await using (var q = operatorDb.CreateCommand("INSERT INTO identity_authority.role_bindings VALUES($1,$2)"))
{ q.Parameters.AddWithValue(tenant); q.Parameters.AddWithValue(NpgsqlDbType.Jsonb, AuthorityCodec.Serialize(roleBinding)); await q.ExecuteNonQueryAsync(); }
NpgsqlDataSource Login(string name) => NpgsqlDataSource.Create(new NpgsqlConnectionStringBuilder(connectionString) { Username = name }.ConnectionString);
await using var adminDb = Login(adminName); await using var providerDb = Login(providerName); await using var readDb = Login(readerName); await using var runtimeDb = Login(runtimeName);
var clock = new AdvancingClock();
var audit = new PostgreSqlSecurityAudit(auditBinding, clock);
var admin = new PostgreSqlIdentityAuthority(adminDb, audit, roleBinding, clock);
var provider = new PostgreSqlIdentityAuthority(providerDb, audit, roleBinding, clock);
var read = new PostgreSqlIdentityAuthority(readDb, audit, roleBinding, clock);
var scope = new HumanScope(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
await using (var q = operatorDb.CreateCommand("WITH c AS (INSERT INTO identity_authority.customers VALUES($1)), p AS (INSERT INTO identity_authority.projects VALUES($1,$2)), e AS (INSERT INTO identity_authority.environments VALUES($1,$2,$3)) INSERT INTO identity_authority.assessments VALUES($1,$2,$3,$4)"))
{ q.Parameters.AddWithValue(scope.CustomerId); q.Parameters.AddWithValue(scope.ProjectId); q.Parameters.AddWithValue(scope.EnvironmentId); q.Parameters.AddWithValue(scope.AssessmentId); await q.ExecuteNonQueryAsync(); }
AuthorityCommandV1 Command(SessionSubject subject, long revision, AuthorityOperation operation, SubjectEnrollmentV1? enrollment = null, HumanAssignmentV1? assignment = null, GuestLifecycleV1? guest = null)
{
    var attribution = enrollment?.Attribution ?? assignment?.Attribution ?? guest?.Attribution;
    var decisionId = operation == AuthorityOperation.ActivateEnrollment ? Guid.NewGuid() : attribution?.ApprovedDecisionId ?? Guid.NewGuid();
    var decision = new AuthorityDecisionV1("authority-decision-v1", Guid.NewGuid(), revision, subject, assignment?.Scope, operation,
        decisionId, AuthorityReason.ApprovedOnboarding, administrator, new string('0', 64));
    var command = new AuthorityCommandV1("authority-command-v1", decision, enrollment, assignment, guest);
    return command with { Decision = decision with { PayloadSha256 = AuthorityCodec.PayloadDigest(command) } };
}
async Task<SessionSubject> Enroll(bool external = false)
{
    var subject = new SessionSubject(tenant, Guid.NewGuid()); var now = clock.GetUtcNow();
    var enrollment = new SubjectEnrollmentV1("subject-enrollment-v1", subject, 1, EnrollmentLifecycle.Pending,
        external ? OrganizationalOrigin.ExternalOrganizational : OrganizationalOrigin.InternalOrganizational,
        external ? Guid.NewGuid() : tenant, now, new(Guid.NewGuid(), administrator, now));
    var pending = Command(subject, 0, AuthorityOperation.EnrollPending, enrollment);
    var receipt = await admin.ExecuteAsync(pending);
    Check(receipt.Revision == 1 && receipt.SecurityVersion == 1, "Pending enrollment atomic receipt");
    var retry = await admin.ExecuteAsync(pending);
    Check(retry == receipt || retry.OperationId == receipt.OperationId && retry.EventIds.SequenceEqual(receipt.EventIds), "Same command resolves immutable receipt before revision check");
    await admin.ExecuteAsync(Command(subject, 1, AuthorityOperation.ActivateEnrollment, enrollment with { Revision = 2, Lifecycle = EnrollmentLifecycle.Active }));
    var now2 = clock.GetUtcNow();
    var assignment = new HumanAssignmentV1("human-assignment-v1", Guid.NewGuid(), subject, scope, HumanRole.Consultant, true,
        now2.AddSeconds(-1), now2.AddDays(1), ["normalized"], [], 1, new(Guid.NewGuid(), administrator, now2));
    await admin.ExecuteAsync(Command(subject, 2, AuthorityOperation.SetAssignment, assignment: assignment));
    return subject;
}
ProviderReadResultV1 Observation(AuthoritySnapshotV1 snapshot, long sequence = 1, bool enabled = true, Guid? role = null, HomeStatusEvidenceV1? home = null)
{
    var now = clock.GetUtcNow();
    return new(new("provider-observation-v1", snapshot.Enrollment.Subject, roleBinding.ClientId, roleBinding.ResourceServicePrincipalId,
        sequence, now.AddMilliseconds(-2), now.AddMilliseconds(-1), snapshot.Enrollment.Revision, snapshot.SecurityVersion,
        snapshot.Enrollment.Subject.ObjectId, enabled, ProviderUserType.Member, home is null ? null : InvitationState.Accepted,
        now.AddHours(-1), [role ?? roleBinding.AppRoles[0].AppRoleId], true, Guid.NewGuid()), home);
}
async Task<long> Count(string table)
{ await using var q = operatorDb.CreateCommand("SELECT count(*) FROM " + table); return (long)(await q.ExecuteScalarAsync())!; }
var subject = await Enroll();
var snapshot = (await read.ReadAsync(subject))!;
Check(snapshot.Enrollment.Attribution.ApprovedDecisionId != Guid.Empty && snapshot.Enrollment.Revision == 3 && snapshot.SecurityVersion == 3, "One aggregate revision/security version authority");
Check(!await read.IsEligibleAsync(subject, default), "Missing provider evidence denies");
var observation = Observation(snapshot);
var publishId = Guid.NewGuid();
var published = await provider.PublishAsync(observation, publishId);
Check((await provider.PublishAsync(observation, publishId)).EventIds.SequenceEqual(published.EventIds), "Provider receipt retry does not refresh or duplicate event");
snapshot = (await read.ReadAsync(subject))!;
Check(await read.IsEligibleAsync(subject, default), "Current exact provider and product assignment admits");
Check(read.ExactAssignments(snapshot, scope, "normalized", clock.GetUtcNow()).Length == 1, "Exact category and scope assignment projection");
foreach (var wrong in new[] { scope with { CustomerId = Guid.NewGuid() }, scope with { ProjectId = Guid.NewGuid() }, scope with { EnvironmentId = Guid.NewGuid() }, scope with { AssessmentId = Guid.NewGuid() } })
    Check(read.ExactAssignments(snapshot, wrong, "normalized", clock.GetUtcNow()).Length == 0, "Cross-scope denial before customer lookup");
Check(read.ExactAssignments(snapshot, scope, "protected", clock.GetUtcNow()).Length == 0, "Denied category never projected");
var runtimeAuthority = new PostgreSqlIdentityAuthority(runtimeDb, audit, roleBinding, clock);
var protector = new EphemeralDataProtectionProvider();
var ticketStore = new PostgreSqlTicketStore(runtimeDb, protector, clock, runtimeAuthority, audit);
AuthenticationTicket Ticket(SessionSubject target, long version) => new(new ClaimsPrincipal(new ClaimsIdentity([
    new Claim("tid", target.TenantId.ToString()), new Claim("oid", target.ObjectId.ToString()), new Claim("roles", "PilotConsultant")], "Cookie", "oid", "roles")),
    new AuthenticationProperties(new Dictionary<string, string?>
    {
        [SessionTicket.AuthenticatedUtc] = clock.GetUtcNow().ToString("O"),
        [SessionTicket.ProviderCheckedUtc] = clock.GetUtcNow().ToString("O"),
        [SessionTicket.SecurityVersion] = version.ToString(System.Globalization.CultureInfo.InvariantCulture),
        [SessionTicket.MfaCaVerified] = "false"
    }), "Cookie");
var ticketKey = await ticketStore.StoreAsync(Ticket(subject, snapshot.SecurityVersion));
Check(await ticketStore.RetrieveAsync(ticketKey) is not null, "Actual restricted audited store composes transaction-bound product authority");
await using (var conn = await runtimeDb.OpenConnectionAsync())
await using (var tx = await conn.BeginTransactionAsync())
{
    await using var lockSubject = new NpgsqlCommand("SELECT identity_authority.lock_subject($1,$2)", conn, tx);
    lockSubject.Parameters.AddWithValue(subject.TenantId); lockSubject.Parameters.AddWithValue(subject.ObjectId); await lockSubject.ExecuteScalarAsync();
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
    Check(await read.IsEligibleAsync(subject, conn, tx, clock.GetUtcNow(), timeout.Token), "Admission reuses same subject transaction without self-deadlock");
    Check(!await read.IsEligibleAsync(subject, conn, tx, observation.Observation.ResourceCutoffUtc.AddTicks(-1), default), "Original authentication before cutoff denies in composed transaction");
}
var priorEvents = await Count("security_audit.events");
foreach (var db in new[] { adminDb, providerDb, readDb, runtimeDb })
    foreach (var forbidden in new[] { "UPDATE identity_authority.enrollments SET lifecycle='Active'", "DELETE FROM identity_authority.assignments", "UPDATE security_audit.events SET sequence=1", "DELETE FROM security_audit.operation_receipts", "SELECT * FROM identity_authority.writer_bindings" })
        await Denied(async () => { await using var q = db.CreateCommand(forbidden); await q.ExecuteNonQueryAsync(); }, "Actual restricted-login direct authority/audit bypass denied");
await Denied(() => new PostgreSqlIdentityAuthority(providerDb, audit, roleBinding, clock).ExecuteAsync(Command(subject, 3, AuthorityOperation.SuspendSubject)), "Provider cannot call administration");
await Denied(() => new PostgreSqlIdentityAuthority(adminDb, audit, roleBinding, clock).PublishAsync(Observation(snapshot, 2), Guid.NewGuid()), "Administrator cannot publish provider state");
var bogusScope = scope with { CustomerId = Guid.NewGuid() };
var badAssignment = snapshot.Assignments[0] with { AssignmentId = Guid.NewGuid(), Scope = bogusScope, Revision = 1, Attribution = new(Guid.NewGuid(), administrator, clock.GetUtcNow()) };
await Denied(() => admin.ExecuteAsync(Command(subject, 3, AuthorityOperation.SetAssignment, assignment: badAssignment)), "Ownership FK prevents cross-customer projection");
Check((await read.ReadAsync(subject))!.SecurityVersion == 3 && await Count("security_audit.events") == priorEvents, "Failed ownership rolls back authority and audit");
var duplicateAssignment = snapshot.Assignments[0] with { AssignmentId = Guid.NewGuid(), Revision = 1, Attribution = new(Guid.NewGuid(), administrator, clock.GetUtcNow()) };
await Denied(() => admin.ExecuteAsync(Command(subject, 3, AuthorityOperation.SetAssignment, assignment: duplicateAssignment)), "Duplicate role/scope cannot merge grants");
var commands = Enumerable.Range(0, 12).Select(_ => Command(subject, 3, AuthorityOperation.SuspendSubject)).ToArray();
var winners = await Task.WhenAll(commands.Select(async c => { try { await admin.ExecuteAsync(c); return true; } catch (Exception e) when (e is InvalidOperationException or NpgsqlException) { return false; } }));
Check(winners.Count(x => x) == 1, "Concurrent same aggregate revision has one audited winner");
snapshot = (await read.ReadAsync(subject))!;
Check(snapshot.SecurityVersion == 4 && snapshot.Enrollment.Revision == 4 && !await read.IsEligibleAsync(subject, default), "Known product suspension denies next request");
Check(await ticketStore.RetrieveAsync(ticketKey) is null, "Authority command atomically revokes actual audited ticket");
await Denied(() => provider.PublishAsync(observation, Guid.NewGuid()), "Stale refresh cannot resurrect suspension");
var winner = commands[Array.FindIndex(winners, x => x)];
var changed = winner with { Decision = winner.Decision with { Reason = AuthorityReason.Incident } };
changed = changed with { Decision = changed.Decision with { PayloadSha256 = AuthorityCodec.PayloadDigest(changed) } };
await Denied(() => admin.ExecuteAsync(changed), "Same operation different digest denies");
var terminal = Command(subject, 4, AuthorityOperation.RevokeSubject);
await admin.ExecuteAsync(terminal);
snapshot = (await read.ReadAsync(subject))!;
await Denied(() => admin.ExecuteAsync(Command(subject, 5, AuthorityOperation.ActivateEnrollment,
    snapshot.Enrollment with { Revision = 6, Lifecycle = EnrollmentLifecycle.Active })), "Revoked enrollment terminal");
var failureSubject = await Enroll(); var failureSnapshot = (await read.ReadAsync(failureSubject))!;
var failureEvents = await Count("security_audit.events");
var wrongAudit = new PostgreSqlSecurityAudit(auditBinding with { StreamId = Guid.NewGuid() }, clock);
await Denied(() => new PostgreSqlIdentityAuthority(adminDb, wrongAudit, roleBinding, clock).ExecuteAsync(Command(failureSubject, 3, AuthorityOperation.SuspendSubject)), "Audit head failure denies authority mutation");
Check((await read.ReadAsync(failureSubject))!.SecurityVersion == 3 && await Count("security_audit.events") == failureEvents, "Audit failure rolls back entire mutation");
var after = Observation(failureSnapshot);
foreach (var invalid in new[] { after with { Observation = after.Observation with { StartedAtUtc = clock.GetUtcNow().AddMinutes(-15) } },
    after with { Observation = after.Observation with { ResourceCutoffUtc = clock.GetUtcNow().AddMinutes(1) } },
    after with { Observation = after.Observation with { ClientId = Guid.NewGuid() } }, after with { Observation = after.Observation with { ReturnedSubjectId = Guid.NewGuid() } },
    after with { Observation = after.Observation with { Complete = false } } })
    await Denied(() => provider.PublishAsync(invalid, Guid.NewGuid()), "Incomplete/stale/future/wrong-bound observation denied");
await provider.PublishAsync(after, Guid.NewGuid());
failureSnapshot = (await read.ReadAsync(failureSubject))!;
var second = Observation(failureSnapshot, 2);
var changedRoles = second with { Observation = second.Observation with { AppRoleIds = [roleBinding.AppRoles[1].AppRoleId] } };
await provider.PublishAsync(changedRoles, Guid.NewGuid());
Check((await read.ReadAsync(failureSubject))!.SecurityVersion == 4, "Current provider role change invalidates old sessions");
await Denied(() => provider.PublishAsync(second, Guid.NewGuid()), "Out-of-order captured security version denied");
await admin.ExecuteAsync(Command(failureSubject, 3, AuthorityOperation.SuspendSubject));
failureSnapshot = (await read.ReadAsync(failureSubject))!;
await admin.ExecuteAsync(Command(failureSubject, 4, AuthorityOperation.ActivateEnrollment,
    failureSnapshot.Enrollment with { Revision = 5, Lifecycle = EnrollmentLifecycle.Active }));
failureSnapshot = (await read.ReadAsync(failureSubject))!;
Check(!await read.IsEligibleAsync(failureSubject, default), "Explicit approved resumption still needs current provider evidence");
await provider.PublishAsync(Observation(failureSnapshot, 3, role: roleBinding.AppRoles[1].AppRoleId), Guid.NewGuid());
failureSnapshot = (await read.ReadAsync(failureSubject))!;
Check(await read.IsEligibleAsync(failureSubject, default), "Approved resumption plus new evidence can admit");
var revokedAssignment = failureSnapshot.Assignments[0] with
{
    Active = false,
    Revision = 2,
    Attribution = new(Guid.NewGuid(), administrator, clock.GetUtcNow())
};
await admin.ExecuteAsync(Command(failureSubject, 5, AuthorityOperation.RevokeAssignment, assignment: revokedAssignment));
Check(!await read.IsEligibleAsync(failureSubject, default) && !(await read.ReadAsync(failureSubject))!.Assignments[0].Active,
    "Exact assignment revocation increments authority and denies");
var externalSubject = await Enroll(true);
var externalSnapshot = (await read.ReadAsync(externalSubject))!;
Check(!await read.IsEligibleAsync(externalSubject, default), "External Member without home/lifecycle denies");
var sponsor = await Enroll();
var now = clock.GetUtcNow();
var guest = new GuestLifecycleV1("guest-lifecycle-v1", externalSubject, sponsor, now.AddSeconds(-1), now.AddDays(30), now,
    Guid.NewGuid(), 1, false, new(Guid.NewGuid(), administrator, now));
await admin.ExecuteAsync(Command(externalSubject, 3, AuthorityOperation.ApproveExternalLifecycle, guest: guest));
externalSnapshot = (await read.ReadAsync(externalSubject))!;
var home = new HomeStatusEvidenceV1(externalSubject, externalSnapshot.Enrollment.HomeTenantId, 4, 4, clock.GetUtcNow(), clock.GetUtcNow().AddMinutes(-2), true, true);
var externalObservation = Observation(externalSnapshot, home: home);
await provider.PublishAsync(externalObservation, Guid.NewGuid());
externalSnapshot = (await read.ReadAsync(externalSubject))!;
Check(await read.IsEligibleAsync(externalSubject, default), "Reviewed synthetic external Member enforces lifecycle");
await using (var conn = await runtimeDb.OpenConnectionAsync()) await using (var tx = await conn.BeginTransactionAsync())
    Check(!await read.IsEligibleAsync(externalSubject, conn, tx, home.CutoffUtc.AddTicks(-1), default), "Home cutoff newer than resource cutoff denies original authentication");
var externalKey = await ticketStore.StoreAsync(Ticket(externalSubject, externalSnapshot.SecurityVersion));
Check(await ticketStore.RetrieveAsync(externalKey) is not null, "External Member audited ticket checks exact home lifecycle");
var newerHome = home with { CheckedAtUtc = clock.GetUtcNow(), CutoffUtc = clock.GetUtcNow() };
await provider.PublishAsync(Observation(externalSnapshot, 2, home: newerHome), Guid.NewGuid());
Check(await ticketStore.RetrieveAsync(externalKey) is null, "Advancing home cutoff denies original authentication on next request");
var homeEvents = await Count("security_audit.events");
var newestExternalSnapshot = (await read.ReadAsync(externalSubject))!;
await Denied(() => provider.PublishAsync(Observation(newestExternalSnapshot, 3, home: home with { CheckedAtUtc = clock.GetUtcNow() }), Guid.NewGuid()), "Home cutoff regression cannot resurrect old authentication");
Check(await Count("security_audit.events") == homeEvents && await ticketStore.RetrieveAsync(externalKey) is null, "Home regression publishes no event/freshness and keeps denial");
await admin.ExecuteAsync(Command(externalSubject, 4, AuthorityOperation.MarkExternalChange));
Check(!await read.IsEligibleAsync(externalSubject, default), "Known sponsor or engagement change denies immediately");
var renewedGuest = guest with
{
    EngagementRevision = 2,
    LastReviewedAtUtc = clock.GetUtcNow(),
    Attribution = new(Guid.NewGuid(), administrator, clock.GetUtcNow())
};
await admin.ExecuteAsync(Command(externalSubject, 5, AuthorityOperation.ApproveExternalLifecycle, guest: renewedGuest));
externalSnapshot = (await read.ReadAsync(externalSubject))!;
Check(!await read.IsEligibleAsync(externalSubject, default), "Attributed guest renewal cannot restore stale provider evidence");
var renewedHome = newerHome with { EnrollmentRevision = 6, SecurityVersion = externalSnapshot.SecurityVersion, CheckedAtUtc = clock.GetUtcNow() };
await provider.PublishAsync(Observation(externalSnapshot, 3, home: renewedHome), Guid.NewGuid());
Check(await read.IsEligibleAsync(externalSubject, default), "Guest renewal and fresh exact home binding restore admission");
await admin.ExecuteAsync(Command(sponsor, 3, AuthorityOperation.SuspendSubject));
Check(!await read.IsEligibleAsync(externalSubject, default), "Sponsor suspension denies external next request");
Check(await read.ReadAsync(new SessionSubject(tenant, Guid.NewGuid())) is null, "Unknown subject never auto-enrolled");
// Actual row and stream-head waits cross the conservative provider deadline.
async Task WaitForLock(string fragment)
{
    for (var attempt = 0; attempt < 60; attempt++)
    {
        await using var q = operatorDb.CreateCommand("SELECT count(*) FROM pg_stat_activity WHERE datname=current_database() AND wait_event_type='Lock' AND query LIKE $1");
        q.Parameters.AddWithValue("%" + fragment + "%");
        if ((long)(await q.ExecuteScalarAsync())! > 0) { checks++; return; }
        await Task.Delay(20);
    }
    throw new Exception("Expected actual observed PostgreSQL lock wait");
}
foreach (var head in new[] { false, true })
{
    var waitSubject = await Enroll(); var waitSnapshot = (await read.ReadAsync(waitSubject))!;
    var stale = Observation(waitSnapshot);
    stale = stale with { Observation = stale.Observation with { StartedAtUtc = clock.GetUtcNow().AddMinutes(-15).AddSeconds(1) } };
    await using var held = await operatorDb.OpenConnectionAsync(); await using var tx = await held.BeginTransactionAsync();
    await using var lockRow = new NpgsqlCommand(head ? "SELECT stream_id FROM security_audit.streams WHERE stream_id=$1 FOR UPDATE" : "SELECT object_id FROM identity_sessions.subjects WHERE object_id=$1 FOR UPDATE", held, tx);
    lockRow.Parameters.AddWithValue(head ? auditBinding.StreamId : waitSubject.ObjectId); await lockRow.ExecuteScalarAsync();
    var beforeCount = await Count("security_audit.events");
    var publication = provider.PublishAsync(stale, Guid.NewGuid());
    await WaitForLock(head ? "security_audit.lock_head" : "identity_authority.lock_subject");
    await Task.Delay(1150); await tx.CommitAsync();
    await Denied(async () => await publication, "Conservative start expired during actual row/head wait");
    Check(await Count("security_audit.events") == beforeCount && !await read.IsEligibleAsync(waitSubject, default), "Expiry rolls back observation/freshness/event/receipt after wait");
}
// Restricted direct SQL calls cannot bypass closed command parsing or durable receipt.
var directSubject = await Enroll(); var directSnapshot = (await read.ReadAsync(directSubject))!;
var direct = Command(directSubject, 3, AuthorityOperation.SuspendSubject);
string RawCommand(string json, string path, string raw)
{
    var root = JsonNode.Parse(json)!;
    var parts = path.Split('.'); JsonNode parent = root;
    foreach (var part in parts[..^1]) parent = parent[part]!;
    parent[parts[^1]] = JsonNode.Parse(raw);
    root["decision"]!["payloadSha256"] = new string('0', 64);
    using var document = JsonDocument.Parse(root.ToJsonString()); using var buffer = new MemoryStream();
    void Canonical(Utf8JsonWriter writer, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        { writer.WriteStartObject(); foreach (var property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)) { writer.WritePropertyName(property.Name); Canonical(writer, property.Value); } writer.WriteEndObject(); }
        else if (element.ValueKind == JsonValueKind.Array)
        { writer.WriteStartArray(); foreach (var item in element.EnumerateArray()) Canonical(writer, item); writer.WriteEndArray(); }
        else element.WriteTo(writer);
    }
    using (var writer = new Utf8JsonWriter(buffer)) Canonical(writer, document.RootElement);
    root["decision"]!["payloadSha256"] = SecurityAuditCanonical.Hash(Encoding.UTF8.GetString(buffer.ToArray()));
    return root.ToJsonString();
}
async Task SqlRefused(NpgsqlDataSource db, string sql, string error, params object[] parameters)
{
    try
    {
        await using var q = db.CreateCommand(sql);
        foreach (var parameter in parameters) q.Parameters.AddWithValue(parameter is string ? NpgsqlDbType.Jsonb : NpgsqlDbType.Uuid, parameter);
        await q.ExecuteNonQueryAsync(); throw new Exception("Expected immediate restricted SQL rejection");
    }
    catch (PostgresException exception) when (exception.MessageText == error || error == "overflow" && exception.SqlState == "22003") { checks++; }
}
var rawPendingSubject = new SessionSubject(tenant, Guid.NewGuid()); var rawNow = clock.GetUtcNow();
var rawEnrollment = new SubjectEnrollmentV1("subject-enrollment-v1", rawPendingSubject, 1, EnrollmentLifecycle.Pending,
    OrganizationalOrigin.InternalOrganizational, tenant, rawNow, new(Guid.NewGuid(), administrator, rawNow));
var rawPending = Command(rawPendingSubject, 0, AuthorityOperation.EnrollPending, rawEnrollment);
var rawAssignment = directSnapshot.Assignments[0] with { Revision = 2, Attribution = new(Guid.NewGuid(), administrator, rawNow) };
foreach (var (command, path) in new[] { (rawPending, "decision.expectedRevision"), (rawPending, "enrollment.revision"),
    (Command(directSubject, 3, AuthorityOperation.SetAssignment, assignment: rawAssignment), "assignment.revision"),
    (Command(externalSubject, 6, AuthorityOperation.ApproveExternalLifecycle, guest: renewedGuest), "guest.engagementRevision") })
    foreach (var (raw, error) in new[] { ("1.2", "authority integer denied"), ("9223372036854775808", "overflow") })
        await SqlRefused(adminDb, "SELECT * FROM identity_authority.apply_command($1)", error,
            RawCommand(AuthorityCodec.Serialize(command), path, raw));
await SqlRefused(adminDb, "SELECT * FROM identity_authority.apply_command($1)", "authority payload digest denied",
    AuthorityCodec.Serialize(direct).Replace(direct.Decision.PayloadSha256, new string('f', 64), StringComparison.Ordinal));
Check((await read.ReadAsync(directSubject))!.SecurityVersion == directSnapshot.SecurityVersion, "Raw integer and mismatched digest commands change no authority");
async Task ProviderRefused(JsonNode raw, string error, bool wrongDigest = false)
{
    try
    {
        await using var q = providerDb.CreateCommand("SELECT * FROM identity_authority.publish_provider($1,$2,$3,$4)");
        q.Parameters.AddWithValue(NpgsqlDbType.Jsonb, raw["observation"]!.ToJsonString());
        q.Parameters.AddWithValue(NpgsqlDbType.Jsonb, raw["homeStatus"] is { } rawHome ? rawHome.ToJsonString() : DBNull.Value);
        q.Parameters.AddWithValue(Guid.NewGuid()); q.Parameters.AddWithValue(wrongDigest ? new string('f', 64) : SecurityAuditCanonical.Hash(raw.ToJsonString()));
        await q.ExecuteNonQueryAsync(); throw new Exception("Expected immediate provider SQL rejection");
    }
    catch (PostgresException exception) when (exception.MessageText == error || error == "overflow" && exception.SqlState == "22003") { checks++; }
}
var rawExternalSnapshot = (await read.ReadAsync(externalSubject))!;
var rawHomeEvidence = renewedHome with { CheckedAtUtc = clock.GetUtcNow() };
foreach (var path in new[] { "observation.sequence", "observation.enrollmentRevision", "observation.securityVersion", "homeStatus.enrollmentRevision", "homeStatus.securityVersion" })
    foreach (var (number, error) in new[] { ("1.2", "authority integer denied"), ("9223372036854775808", "overflow") })
    {
        var raw = JsonNode.Parse(AuthorityCodec.Serialize(Observation(rawExternalSnapshot, 4, home: rawHomeEvidence)))!;
        var parts = path.Split('.'); raw[parts[0]]![parts[1]] = JsonNode.Parse(number);
        await ProviderRefused(raw, error);
    }
await ProviderRefused(JsonNode.Parse(AuthorityCodec.Serialize(Observation(directSnapshot)))!, "provider payload digest denied", true);
Check((await read.ReadAsync(directSubject))!.SecurityVersion == directSnapshot.SecurityVersion, "Raw provider integers and mismatched digest change no authority");
foreach (var badJson in new[] { AuthorityCodec.Serialize(direct).Replace("\"authority-command-v1\"", "null"),
    AuthorityCodec.Serialize(direct).Replace("\"SuspendSubject\"", "\"Unknown\""),
    AuthorityCodec.Serialize(direct).Replace("\"expectedRevision\":3", "\"expectedRevision\":null"),
    AuthorityCodec.Serialize(direct).Replace("\"assignment\":null", "\"assignment\":{}") })
    await Denied(async () => { await using var q = adminDb.CreateCommand("SELECT * FROM identity_authority.apply_command($1)"); q.Parameters.AddWithValue(NpgsqlDbType.Jsonb, badJson); await q.ExecuteNonQueryAsync(); }, "Restricted SQL closed shape cannot bypass parser");
await Denied(async () =>
{
    await using var q = adminDb.CreateCommand("SELECT * FROM identity_authority.apply_command($1)");
    q.Parameters.AddWithValue(NpgsqlDbType.Jsonb, AuthorityCodec.Serialize(direct)); await q.ExecuteNonQueryAsync();
}, "Direct command without audit event/receipt cannot commit");
Check((await read.ReadAsync(directSubject))!.SecurityVersion == directSnapshot.SecurityVersion, "Unaudited direct mutation rolled back");
// Privileged corruption fixture proves the deferred guard does not treat missing
// lowercase receipt fields as SQL NULL/false. It grants no ordinary writer bypass.
await using (var bind = operatorDb.CreateCommand("INSERT INTO identity_authority.writer_bindings VALUES($1,'Administrator',$2,$3)"))
{ bind.Parameters.AddWithValue(settings.Username!); bind.Parameters.AddWithValue(administrator.TenantId); bind.Parameters.AddWithValue(administrator.ObjectId); await bind.ExecuteNonQueryAsync(); }
foreach (var receiptJson in new[] { "{}", "{\"SecurityVersion\":4,\"ResultingRevision\":4}", "{\"securityVersion\":null,\"resultingRevision\":null}" })
    await Denied(async () =>
    {
        await using var conn = await operatorDb.OpenConnectionAsync(); await using var tx = await conn.BeginTransactionAsync();
        await using (var apply = new NpgsqlCommand("SELECT * FROM identity_authority.apply_command($1)", conn, tx))
        { apply.Parameters.AddWithValue(NpgsqlDbType.Jsonb, AuthorityCodec.Serialize(direct)); await apply.ExecuteNonQueryAsync(); }
        var request = new OperationReceiptRequestV1(direct.Decision.CommandId, auditBinding.Writer, SecurityAuditActorKind.Human,
            administrator, SecurityAuditAction.AuthorityChanged, directSubject, direct.Decision.PayloadSha256);
        await using (var corrupt = new NpgsqlCommand("INSERT INTO security_audit.operation_receipts VALUES($1,$2,$3,$4)", conn, tx))
        {
            corrupt.Parameters.AddWithValue(direct.Decision.CommandId); corrupt.Parameters.AddWithValue(SecurityAuditCanonical.Request(request));
            corrupt.Parameters.AddWithValue(receiptJson); corrupt.Parameters.AddWithValue(new[] { Guid.NewGuid() }); await corrupt.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }, "Missing/null/Pascal receipt fields cannot pass deferred guard");
Check((await read.ReadAsync(directSubject))!.SecurityVersion == directSnapshot.SecurityVersion, "Malformed receipt leaves authority unchanged");
Console.WriteLine($"PASS {checks} actual PostgreSQL authority/audit/role/concurrency assertions");

sealed class AdvancingClock : TimeProvider
{
    public TimeSpan Offset { get; set; }
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow + Offset;
}
