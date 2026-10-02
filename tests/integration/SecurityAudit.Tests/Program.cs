using System.Security.Claims;
using System.Text.Json;
using IdentitySessions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Npgsql;

if (args is not ["--reset-synthetic-schema"])
    throw new InvalidOperationException("Explicit synthetic schema reset required.");
var supplied = Environment.GetEnvironmentVariable("IGA_AUDIT_TEST_CONNECTION") ?? throw new InvalidOperationException("Synthetic audit connection required.");
var settings = new NpgsqlConnectionStringBuilder(supplied);
if (settings.Host is not ("127.0.0.1" or "localhost") || settings.Database is null || !settings.Database.StartsWith("iga_synthetic_", StringComparison.Ordinal))
    throw new InvalidOperationException("Only named loopback synthetic databases allowed.");
await using var owner = NpgsqlDataSource.Create(supplied);
var count = 0;
void Check(bool value, string reason) { if (!value) throw new Exception(reason); count++; }
async Task Sql(string sql, params object[] values)
{
    await using var command = owner.CreateCommand(sql);
    foreach (var value in values) command.Parameters.AddWithValue(value);
    await command.ExecuteNonQueryAsync();
}
async Task<long> Scalar(string sql)
{ await using var c = owner.CreateCommand(sql); return Convert.ToInt64(await c.ExecuteScalarAsync()); }
async Task Refused(Func<Task> task, string reason)
{
    try { await task(); throw new Exception(reason); }
    catch (Exception exception) when (exception is PostgresException or InvalidOperationException or JsonException) { count++; }
}
await Sql("DROP SCHEMA IF EXISTS security_audit CASCADE; DROP SCHEMA IF EXISTS identity_sessions CASCADE;");
foreach (var migration in new[] { "001-initial.sql", "002-authentication-context.sql", "003-atomic-audit.sql" })
{
    using var resource = typeof(PostgreSqlTicketStore).Assembly.GetManifestResourceStream("IdentitySessions." + migration)!;
    using var reader = new StreamReader(resource);
    await Sql(await reader.ReadToEndAsync());
}
// Explicit local role provisioning only; migration003 creates/grants no LOGIN.
await Sql("""
DO $$ BEGIN
 IF NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='iga_audit_function_owner') THEN CREATE ROLE iga_audit_function_owner NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS; END IF;
 IF NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='iga_audit_runtime') THEN CREATE ROLE iga_audit_runtime LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS; END IF;
 IF NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='iga_audit_admin') THEN CREATE ROLE iga_audit_admin LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS; END IF;
 IF NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='iga_audit_reader') THEN CREATE ROLE iga_audit_reader LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS; END IF;
 IF NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='iga_audit_lifecycle') THEN CREATE ROLE iga_audit_lifecycle LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS; END IF;
 IF NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='iga_audit_witness') THEN CREATE ROLE iga_audit_witness LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS; END IF;
END $$;
GRANT USAGE ON SCHEMA security_audit,identity_sessions TO iga_audit_function_owner,iga_audit_runtime,iga_audit_admin,iga_audit_reader,iga_audit_lifecycle,iga_audit_witness;
GRANT SELECT,INSERT,UPDATE,DELETE ON ALL TABLES IN SCHEMA security_audit,identity_sessions TO iga_audit_function_owner;
DO $$ DECLARE f record; BEGIN
 FOR f IN SELECT p.oid::regprocedure AS signature FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname='security_audit'
 LOOP EXECUTE 'ALTER FUNCTION '||f.signature||' OWNER TO iga_audit_function_owner'; END LOOP;
END $$;
REVOKE ALL ON identity_sessions.tickets FROM iga_audit_runtime;
GRANT SELECT ON security_audit.visible_events TO iga_audit_reader;
GRANT SELECT ON security_audit.events,security_audit.tombstones,security_audit.streams,security_audit.operation_receipts TO iga_audit_witness;
GRANT EXECUTE ON FUNCTION security_audit.lock_subject(uuid,uuid),security_audit.lock_ticket(text),security_audit.lookup_ticket_subject(text),security_audit.touch_ticket(text,timestamptz),security_audit.issue_ticket(text,uuid,uuid,uuid,bigint,timestamptz,timestamptz,bytea,uuid),security_audit.revoke_ticket(text,timestamptz,uuid),security_audit.lock_head(uuid,text,uuid),security_audit.append_event(uuid,uuid,text,text),security_audit.read_receipt(uuid),security_audit.append_receipt(uuid,text,text,uuid[]) TO iga_audit_runtime;
GRANT EXECUTE ON FUNCTION security_audit.lock_subject(uuid,uuid),security_audit.revoke_reference(uuid,uuid,uuid,timestamptz,uuid),security_audit.revoke_subject(uuid,uuid,boolean,timestamptz,uuid),security_audit.lock_head(uuid,text,uuid),security_audit.append_event(uuid,uuid,text,text),security_audit.read_receipt(uuid),security_audit.append_receipt(uuid,text,text,uuid[]) TO iga_audit_admin;
GRANT EXECUTE ON FUNCTION security_audit.set_hold(uuid,uuid,timestamptz,uuid),security_audit.release_hold(uuid,uuid,uuid),security_audit.soft_delete_event(uuid,timestamptz,uuid),security_audit.purge_event(uuid,timestamptz,uuid) TO iga_audit_lifecycle;
GRANT EXECUTE ON FUNCTION security_audit.record_checkpoint(uuid,uuid,text,uuid,bigint,text,timestamptz) TO iga_audit_witness;
""");
NpgsqlDataSource As(string role) { var s = new NpgsqlConnectionStringBuilder(supplied) { Username = role }; return NpgsqlDataSource.Create(s.ConnectionString); }
await using var runtime = As("iga_audit_runtime"); await using var admin = As("iga_audit_admin");
await using var readerRole = As("iga_audit_reader"); await using var lifecycle = As("iga_audit_lifecycle"); await using var witnessRole = As("iga_audit_witness");
async Task RoleSql(NpgsqlDataSource source, string sql, params object[] values)
{ await using var c = source.CreateCommand(sql); foreach (var v in values) c.Parameters.AddWithValue(v); await c.ExecuteNonQueryAsync(); }
var clock = new Clock(); var admission = new Admission(); var writer = new SessionSubject(Guid.NewGuid(), Guid.NewGuid());
var binding = new SecurityAuditBindingV1(Guid.NewGuid(), "synthetic-audit", Guid.NewGuid(), writer, Guid.NewGuid());
await Sql("INSERT INTO security_audit.streams(stream_id,environment_id,writer_binding_reference,writer_tenant_id,writer_object_id,application_client_id) VALUES($1,$2,$3,$4,$5,$6)",
    binding.StreamId, binding.EnvironmentId, binding.WriterBindingReference, writer.TenantId, writer.ObjectId, binding.ApplicationClientId);
await Sql("INSERT INTO security_audit.writer_roles VALUES($1,'iga_audit_runtime',$2,ARRAY['SessionIssued','SessionRevoked','SessionRotated','AuthenticationDenied','AuthenticationFailed']),($1,'iga_audit_admin',$2,ARRAY['SessionRevoked','SubjectRevoked','AuthorityChanged'])", binding.StreamId, binding.WriterBindingReference);
await Sql("INSERT INTO security_audit.reader_scopes VALUES('iga_audit_reader',$1,true,NULL,NULL,NULL,NULL)", binding.StreamId);
await Sql("INSERT INTO security_audit.witness_bindings VALUES('iga_audit_witness',$1)", binding.StreamId);
var lifecycleAuthority = Guid.NewGuid();
await Sql("INSERT INTO security_audit.lifecycle_bindings VALUES('iga_audit_lifecycle',$1)", lifecycleAuthority);
// Function owner permissions include tables introduced after their creation.
await Sql("GRANT SELECT,INSERT,UPDATE,DELETE ON ALL TABLES IN SCHEMA security_audit,identity_sessions TO iga_audit_function_owner");
var audit = new PostgreSqlSecurityAudit(binding, clock);
var dir = Path.Combine(Path.GetTempPath(), "iga-audit-keys-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
try
{
    var keys = DataProtectionProvider.Create(new DirectoryInfo(dir), c => c.SetApplicationName("SyntheticAtomicAudit"));
    var store = new PostgreSqlTicketStore(runtime, keys, clock, admission, audit);
    var legacy = new PostgreSqlSessionAuthority(owner, clock); // test-only fixture provisioning; no production authority claim
    var authority = new PostgreSqlAuditedSessionAuthority(admin, clock, audit);
    async Task<SessionSubject> Subject()
    { var subject = new SessionSubject(writer.TenantId, Guid.NewGuid()); await legacy.ProvisionAsync(subject, true, clock.GetUtcNow(), DateTimeOffset.UnixEpoch); return subject; }
    AuthenticationTicket Ticket(SessionSubject subject, long version = 1) => new(new ClaimsPrincipal(new ClaimsIdentity([
        new Claim("tid", subject.TenantId.ToString("D")), new Claim("oid", subject.ObjectId.ToString("D")), new Claim("roles", "PilotConsultant")], "Cookie", "oid", "roles")),
        new AuthenticationProperties(new Dictionary<string, string?> { [SessionTicket.AuthenticatedUtc] = clock.GetUtcNow().ToString("O"), [SessionTicket.ProviderCheckedUtc] = clock.GetUtcNow().ToString("O"), [SessionTicket.SecurityVersion] = version.ToString(), [SessionTicket.MfaCaVerified] = "false" }), "Cookie");
    var a = await Subject(); var key = await store.StoreAsync(Ticket(a));
    Check(store.HasAtomicAudit && await store.RetrieveAsync(key) is not null, "Restricted runtime persisted/retrieved audited ticket");
    Check(await Scalar("SELECT count(*) FROM security_audit.events") == 1 && await Scalar("SELECT count(*) FROM security_audit.operation_receipts") == 1, "Issuance event and receipt committed together");
    var replacement = await store.RotateAsync(key, Ticket(a));
    Check(replacement is not null && await store.RetrieveAsync(key) is null && await store.RetrieveAsync(replacement) is not null, "Restricted audited rotation is atomic");
    Check(await Scalar("SELECT count(*) FROM security_audit.events") == 2, "Exactly one rotation event");
    await store.RemoveAsync(replacement!); Check(await store.RetrieveAsync(replacement!) is null, "Restricted exact local revoke durable");
    await store.RemoveAsync(replacement!); Check(await Scalar("SELECT count(*) FROM security_audit.events") == 3, "Repeated local removal creates no duplicate mutation");

    foreach (var sql in new[] { "UPDATE identity_sessions.subjects SET active=true", "DELETE FROM identity_sessions.tickets", "INSERT INTO security_audit.events SELECT * FROM security_audit.events", "UPDATE security_audit.events SET canonical_event='{}'", "DELETE FROM security_audit.operation_receipts", "SELECT security_audit.append_event_checked_binding(NULL,NULL,'{}','x')", "SELECT security_audit.revoke_subject(NULL,NULL,false,now(),NULL)", "SELECT security_audit.purge_event(NULL,now(),NULL)" })
        await Refused(() => RoleSql(runtime, sql), "Ordinary runtime bypass accepted: " + sql);
    await Refused(() => RoleSql(readerRole, "SELECT security_audit.append_event(NULL,NULL,'{}','x')"), "Reader cannot append");
    await Refused(() => RoleSql(lifecycle, "SELECT security_audit.revoke_subject(NULL,NULL,false,now(),NULL)"), "Lifecycle cannot mutate authority");
    await Refused(() => RoleSql(admin, "SELECT security_audit.purge_event(NULL,now(),NULL)"), "Admin cannot purge audit");
    Check(await Scalar("SELECT count(*) FROM pg_roles WHERE rolname='iga_audit_function_owner' AND NOT rolcanlogin AND NOT rolsuper") == 1, "Distinct owner is NONLOGIN nonsuperuser");

    await Refused(async () =>
    {
        await using var connection = await runtime.OpenConnectionAsync();
        await using var tx = await connection.BeginTransactionAsync();
        await audit.AppendAsync(connection, tx, new(Guid.NewGuid(), Guid.NewGuid(), clock.GetUtcNow(), SecurityAuditActorKind.Human,
            a, Guid.NewGuid(), SecurityAuditAction.SessionIssued, SecurityAuditOutcome.Succeeded, SecurityAuditReason.None, a, Guid.NewGuid(), null, 1));
        await tx.CommitAsync();
    }, "Standalone mutation event committed without receipt");
    await Refused(() => RoleSql(runtime, "SELECT security_audit.revoke_ticket($1,$2,NULL)", SessionTicket.HashKey(key)!, clock.GetUtcNow()), "Null operation bypassed strict revoke");
    await Refused(() => RoleSql(admin, "SELECT security_audit.revoke_subject($1,$2,false,$3,NULL)", a.TenantId, a.ObjectId, clock.GetUtcNow()), "Null operation bypassed strict subject revoke");
    await Refused(() => RoleSql(runtime, "SELECT security_audit.issue_ticket($1,$2,$3,$4,1,$5,$6,$7,NULL)", SessionTicket.HashKey(SessionTicket.NewKey())!, Guid.NewGuid(), a.TenantId, a.ObjectId, clock.GetUtcNow(), clock.GetUtcNow().AddHours(8), new byte[] { 0 }), "Null operation bypassed strict issuance");

    var changedEvent = new SecurityAuditEventV1(Guid.NewGuid(), Guid.NewGuid(), clock.GetUtcNow(), SecurityAuditActorKind.Workload, writer,
        Guid.NewGuid(), SecurityAuditAction.AuthorityChanged, SecurityAuditOutcome.Succeeded, SecurityAuditReason.None, a, null, null, 1);
    await Refused(async () => { await using var c = await runtime.OpenConnectionAsync(); await using var t = await c.BeginTransactionAsync(); await audit.AppendAsync(c, t, changedEvent); }, "Ticket writer appended authority change");
    var closedEvent = changedEvent with { Action = SecurityAuditAction.SessionIssued, SessionReference = Guid.NewGuid(), ActorKind = SecurityAuditActorKind.Human, Actor = a };
    var nextHead = await Scalar("SELECT head_sequence FROM security_audit.streams");
    await using (var headQuery = owner.CreateCommand("SELECT head_sha256 FROM security_audit.streams"))
    {
        var previous = (string)(await headQuery.ExecuteScalarAsync())!;
        var valid = SecurityAuditCanonical.Event(binding, closedEvent, nextHead + 1, previous);
        using var document = JsonDocument.Parse(valid);
        var reordered = JsonSerializer.Serialize(document.RootElement.EnumerateObject().Reverse().ToDictionary(p => p.Name, p => p.Value.Clone(), StringComparer.Ordinal));
        foreach (var noncanonical in new[] { " " + valid, valid.Replace(":", ": ", StringComparison.Ordinal), valid.Replace("synthetic-audit", "synthetic-\\u0061udit", StringComparison.Ordinal), reordered })
        {
            try { await RoleSql(runtime, "SELECT security_audit.append_event($1,$2,$3,$4)", binding.StreamId, binding.WriterBindingReference, noncanonical, SecurityAuditCanonical.Hash(noncanonical)); throw new Exception("Noncanonical SQL audit bytes accepted"); }
            catch (PostgresException exception) when (exception.MessageText == "Noncanonical audit bytes") { count++; }
        }
        var nonfixedUtc = valid.Replace(closedEvent.EventAtUtc.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", System.Globalization.CultureInfo.InvariantCulture), closedEvent.EventAtUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        try { await RoleSql(runtime, "SELECT security_audit.append_event($1,$2,$3,$4)", binding.StreamId, binding.WriterBindingReference, nonfixedUtc, SecurityAuditCanonical.Hash(nonfixedUtc)); throw new Exception("Nonfixed UTC SQL audit value accepted"); }
        catch (PostgresException exception) when (exception.MessageText == "Closed event value refused") { count++; }
        var denied = closedEvent with { Action = SecurityAuditAction.AuthenticationDenied, Outcome = SecurityAuditOutcome.Denied, Reason = SecurityAuditReason.AuthorityDenied, SessionReference = null, SecurityVersion = long.MaxValue };
        var overflow = SecurityAuditCanonical.Event(binding, denied, nextHead + 1, previous).Replace(long.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture), "9223372036854775808", StringComparison.Ordinal);
        try { await RoleSql(runtime, "SELECT security_audit.append_event($1,$2,$3,$4)", binding.StreamId, binding.WriterBindingReference, overflow, SecurityAuditCanonical.Hash(overflow)); throw new Exception("Overflow SQL audit version accepted"); }
        catch (PostgresException exception) when (exception.SqlState == "22003") { count++; }
        foreach (var field in new[] { "actorKind", "action", "securityVersion", "writer", "applicationClientId", "writerBindingReference" })
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(valid)!; node[field] = null;
            var invalid = node.ToJsonString();
            await Refused(() => RoleSql(runtime, "SELECT security_audit.append_event($1,$2,$3,$4)", binding.StreamId, binding.WriterBindingReference, invalid, SecurityAuditCanonical.Hash(invalid)), "SQL null field bypass: " + field);
        }
        var nodeExtra = System.Text.Json.Nodes.JsonNode.Parse(valid)!; nodeExtra["actor"]!["rawClaims"] = "synthetic-prohibited";
        var invalidExtra = nodeExtra.ToJsonString();
        await Refused(() => RoleSql(runtime, "SELECT security_audit.append_event($1,$2,$3,$4)", binding.StreamId, binding.WriterBindingReference, invalidExtra, SecurityAuditCanonical.Hash(invalidExtra)), "SQL arbitrary actor payload accepted");
    }
    var otherBinding = binding with { StreamId = Guid.NewGuid(), WriterBindingReference = Guid.NewGuid(), EnvironmentId = "synthetic-other" };
    await Sql("INSERT INTO security_audit.streams(stream_id,environment_id,writer_binding_reference,writer_tenant_id,writer_object_id,application_client_id) VALUES($1,$2,$3,$4,$5,$6)", otherBinding.StreamId, otherBinding.EnvironmentId, otherBinding.WriterBindingReference, writer.TenantId, writer.ObjectId, otherBinding.ApplicationClientId);
    var otherAudit = new PostgreSqlSecurityAudit(otherBinding, clock);
    await Refused(async () => { await using var c = await runtime.OpenConnectionAsync(); await using var t = await c.BeginTransactionAsync(); await otherAudit.AppendAsync(c, t, closedEvent); }, "Known foreign stream GUID bypassed SESSION_USER binding");
    var otherEvent = closedEvent with { EventId = Guid.NewGuid(), OperationId = Guid.NewGuid(), Action = SecurityAuditAction.AuthenticationDenied, Outcome = SecurityAuditOutcome.Denied, Reason = SecurityAuditReason.AuthorityDenied, SessionReference = null };
    var otherCanonical = SecurityAuditCanonical.Event(otherBinding, otherEvent, 1, new string('0', 64));
    await Sql("INSERT INTO security_audit.events VALUES($1,$2,$3,1,$4,$5,$6,$7)", otherEvent.EventId, otherEvent.OperationId, otherBinding.StreamId, clock.GetUtcNow(), new string('0', 64), SecurityAuditCanonical.Hash(otherCanonical), otherCanonical);
    var otherRequest = new OperationReceiptRequestV1(otherEvent.OperationId, writer, otherEvent.ActorKind, otherEvent.Actor, otherEvent.Action, a, SecurityAuditCanonical.Hash("synthetic-denial"));
    var otherReceipt = new OperationReceiptV1(otherRequest, SecurityAuditOutcome.Denied, clock.GetUtcNow(), null, 1, null, [otherEvent.EventId]);
    try { await RoleSql(runtime, "SELECT security_audit.append_receipt($1,$2,$3,$4)", otherEvent.OperationId, SecurityAuditCanonical.Request(otherRequest), SecurityAuditCanonical.Receipt(otherReceipt), new[] { otherEvent.EventId }); throw new Exception("Foreign stream receipt accepted"); }
    catch (PostgresException exception) when (exception.MessageText == "Receipt writer/action denied") { count++; }
    var actionEvent = otherEvent with { EventId = Guid.NewGuid(), OperationId = Guid.NewGuid() };
    var actionCanonical = SecurityAuditCanonical.Event(binding, actionEvent, 999, new string('0', 64));
    await Sql("INSERT INTO security_audit.events VALUES($1,$2,$3,999,$4,$5,$6,$7)", actionEvent.EventId, actionEvent.OperationId, binding.StreamId, clock.GetUtcNow(), new string('0', 64), SecurityAuditCanonical.Hash(actionCanonical), actionCanonical);
    var actionRequest = otherRequest with { OperationId = actionEvent.OperationId };
    var actionReceipt = otherReceipt with { Request = actionRequest, EventIds = new[] { actionEvent.EventId } };
    try { await RoleSql(admin, "SELECT security_audit.append_receipt($1,$2,$3,$4)", actionEvent.OperationId, SecurityAuditCanonical.Request(actionRequest), SecurityAuditCanonical.Receipt(actionReceipt), new[] { actionEvent.EventId }); throw new Exception("Unauthorized receipt action accepted"); }
    catch (PostgresException exception) when (exception.MessageText == "Receipt writer/action denied") { count++; }
    await Sql("DELETE FROM security_audit.events WHERE event_id=ANY($1)", new[] { otherEvent.EventId, actionEvent.EventId });
    await Sql("DELETE FROM security_audit.streams WHERE stream_id=$1", otherBinding.StreamId);
    await Sql("UPDATE security_audit.reader_scopes SET actor_tenant_id=$1,actor_object_id=$2", a.TenantId, a.ObjectId);
    await using (var actorFiltered = readerRole.CreateCommand("SELECT count(*) FROM security_audit.visible_events"))
        Check((long)(await actorFiltered.ExecuteScalarAsync())! == 3, "Reader actor binding filters other identities");
    await Sql("UPDATE security_audit.reader_scopes SET actor_tenant_id=NULL,actor_object_id=NULL,allow_platform=false,customer_id=$1,project_id=$2", Guid.NewGuid(), Guid.NewGuid());
    await using (var customerFiltered = readerRole.CreateCommand("SELECT count(*) FROM security_audit.visible_events"))
        Check((long)(await customerFiltered.ExecuteScalarAsync())! == 0, "Customer-project audit reader cannot read platform authentication");
    await Sql("UPDATE security_audit.reader_scopes SET allow_platform=true,customer_id=NULL,project_id=NULL");

    // Revoke audit append capability to inject a real PostgreSQL audit failure.
    var failed = await Subject(); var before = await Scalar("SELECT count(*) FROM identity_sessions.tickets");
    await Sql("REVOKE EXECUTE ON FUNCTION security_audit.append_event(uuid,uuid,text,text) FROM iga_audit_runtime");
    await Refused(() => store.StoreAsync(Ticket(failed)), "Unaudited issuance accepted");
    Check(await Scalar("SELECT count(*) FROM identity_sessions.tickets") == before, "Audit failure rolls back issuance");
    await Sql("GRANT EXECUTE ON FUNCTION security_audit.append_event(uuid,uuid,text,text) TO iga_audit_runtime");
    var failureKey = await store.StoreAsync(Ticket(failed));
    await Refused(() => Sql("UPDATE identity_sessions.tickets SET revoked_at=$1 WHERE key_hash=$2", clock.GetUtcNow(), SessionTicket.HashKey(failureKey)!), "Legacy direct mutation bypassed audited ticket receipt");
    await Sql("REVOKE EXECUTE ON FUNCTION security_audit.append_event(uuid,uuid,text,text) FROM iga_audit_runtime");
    await Refused(() => store.RemoveAsync(failureKey), "Unaudited revoke accepted");
    Check(await store.RetrieveAsync(failureKey) is not null, "Audit failure rolls back revocation");
    await Refused(() => store.RotateAsync(failureKey, Ticket(failed)), "Unaudited rotate accepted");
    Check(await store.RetrieveAsync(failureKey) is not null, "Audit failure preserves old rotation target");
    await Sql("GRANT EXECUTE ON FUNCTION security_audit.append_event(uuid,uuid,text,text) TO iga_audit_runtime");

    // Commit acknowledgment loss simulation discards the returned result, then
    // resolves the internal immutable receipt; no cookie or key is replayed.
    var otherExactKey = await store.StoreAsync(Ticket(failed));
    var reference = Guid.Parse((await store.RetrieveAsync(failureKey))!.Properties.Items[SessionTicket.SessionReference]!);
    var operation = Guid.NewGuid(); var digest = SecurityAuditCanonical.Hash($"RevokeExact:{failed.TenantId:D}:{failed.ObjectId:D}:{reference:D}");
    var request = new OperationReceiptRequestV1(operation, writer, SecurityAuditActorKind.Workload, writer,
        SecurityAuditAction.SessionRevoked, failed, digest, OldSessionReference: reference);
    try
    {
        _ = await authority.RevokeExactAsync(request, reference); // actual PostgreSQL COMMIT completed
        throw new IOException("Synthetic acknowledgment discarded after actual COMMIT.");
    }
    catch (IOException) { count++; }
    Check(await store.RetrieveAsync(failureKey) is null && await store.RetrieveAsync(otherExactKey) is not null,
        "Lost acknowledgment never claims rollback; exact target revoked and other session preserved");
    var eventCount = await Scalar("SELECT count(*) FROM security_audit.events");
    var receipt = await authority.RevokeExactAsync(request, reference);
    Check(receipt.Request == request && receipt.NewSessionReference is null && receipt.EventIds.Count == 1, "Unknown acknowledgment resolves metadata only");
    Check(await Scalar("SELECT count(*) FROM security_audit.events") == eventCount, "Receipt retry cannot emit duplicate revoke");
    Console.WriteLine("PASS case: actual PostgreSQL COMMIT followed by deterministic simulated acknowledgment loss; internal metadata-only receipt retry, no credential replay (not actual TCP interruption).");
    await Refused(() => authority.RevokeExactAsync(request with { CommandSha256 = SecurityAuditCanonical.Hash("different") }, reference), "Operation ID digest conflict accepted");
    await Refused(() => authority.RevokeExactAsync(request with { Actor = new SessionSubject(writer.TenantId, Guid.NewGuid()) }, reference), "Receipt actor substitution accepted");
    var subjectRequest = new OperationReceiptRequestV1(Guid.NewGuid(), writer, SecurityAuditActorKind.Workload, writer,
        SecurityAuditAction.SubjectRevoked, failed, PostgreSqlAuditedSessionAuthority.SubjectRevocationDigest(failed, false));
    var wins = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => authority.RevokeSubjectAsync(subjectRequest, false)));
    Check(wins.All(r => r.SecurityVersion == 2 && r.EventIds.SequenceEqual(wins[0].EventIds)), "Concurrent exact retries increment subject once");
    Check((await legacy.ListSessionsAsync(failed)).All(s => s.Revoked), "Subject command revokes all exact subject sessions");
    await Refused(() => authority.RevokeSubjectAsync(subjectRequest, true), "Same operation/digest with changed disable intent accepted");

    var expired = await Subject(); var expiredTicket = Ticket(expired);
    await using (var headConnection = await owner.OpenConnectionAsync())
    await using (var headTransaction = await headConnection.BeginTransactionAsync())
    {
        await using var hold = new NpgsqlCommand("SELECT stream_id FROM security_audit.streams WHERE stream_id=$1 FOR UPDATE", headConnection, headTransaction);
        hold.Parameters.AddWithValue(binding.StreamId); await hold.ExecuteScalarAsync();
        var waiting = store.StoreAsync(expiredTicket); var observed = false;
        for (var attempt = 0; attempt < 100 && !observed; attempt++)
        { await using var q = owner.CreateCommand("SELECT EXISTS(SELECT 1 FROM pg_stat_activity WHERE datname=current_database() AND wait_event_type='Lock' AND query LIKE '%lock_head%')"); observed = (bool)(await q.ExecuteScalarAsync())!; if (!observed) await Task.Delay(10); }
        Check(observed && !waiting.IsCompleted, "Actual stream head contention observed");
        clock.Advance(TimeSpan.FromMinutes(15)); await headTransaction.CommitAsync();
        await Refused(async () => await waiting, "Deadline crossing audit head admitted ticket");
        Check((await legacy.ListSessionsAsync(expired)).Count == 0, "Fresh deadline denial rolls back ticket/event/receipt");
    }

    var distinctSubjects = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Subject()));
    var distinctKeys = await Task.WhenAll(distinctSubjects.Select(subject => store.StoreAsync(Ticket(subject))));
    Check(distinctKeys.Distinct().Count() == 16, "Sixteen distinct-subject writers share ordered stream without inversion/deadlock");

    // Verify the complete chain with an independently captured synthetic witness.
    async Task<(long Sequence, string Digest)> Head()
    { await using var c = witnessRole.CreateCommand("SELECT head_sequence,head_sha256 FROM security_audit.streams"); await using var r = await c.ExecuteReaderAsync(); await r.ReadAsync(); return (r.GetInt64(0), r.GetString(1)); }
    async Task<List<AuditIntegrityEntryV1>> Entries()
    {
        var entries = new List<AuditIntegrityEntryV1>(); await using var c = witnessRole.CreateCommand("SELECT event_id,stream_id,sequence,previous_sha256,event_sha256,canonical_event,NULL::uuid FROM security_audit.events UNION ALL SELECT event_id,stream_id,sequence,previous_sha256,event_sha256,NULL,lifecycle_authority FROM security_audit.tombstones");
        await using var r = await c.ExecuteReaderAsync(); while (await r.ReadAsync()) entries.Add(new(r.GetGuid(0), r.GetGuid(1), r.GetInt64(2), r.GetString(3), r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5), r.IsDBNull(6) ? null : r.GetGuid(6))); return entries;
    }
    async Task<List<OperationReceiptV1>> Receipts()
    { var values = new List<OperationReceiptV1>(); await using var c = witnessRole.CreateCommand("SELECT receipt_canonical FROM security_audit.operation_receipts"); await using var r = await c.ExecuteReaderAsync(); while (await r.ReadAsync()) values.Add(SecurityAuditCanonical.ReadReceipt(r.GetString(0))); return values; }
    var head = await Head(); var checkpoint = new AuditCheckpointV1(binding.StreamId, binding.EnvironmentId, binding.WriterBindingReference, head.Sequence, head.Digest, clock.GetUtcNow(), Guid.NewGuid());
    await RoleSql(witnessRole, "SELECT security_audit.record_checkpoint($1,$2,$3,$4,$5,$6,$7)", checkpoint.WitnessReference, binding.StreamId, binding.EnvironmentId, binding.WriterBindingReference, head.Sequence, head.Digest, clock.GetUtcNow());
    AuditIntegrityVerifier.Verify(binding, checkpoint, head.Sequence, head.Digest, await Entries(), await Receipts()); count++;
    await Refused(() => { AuditIntegrityVerifier.Verify(binding, checkpoint, head.Sequence, head.Digest, (new List<AuditIntegrityEntryV1>(Entries().GetAwaiter().GetResult())).Skip(1).ToList(), Receipts().GetAwaiter().GetResult()); return Task.CompletedTask; }, "Unexplained gap accepted");
    await Refused(() => { AuditIntegrityVerifier.Verify(binding, checkpoint, head.Sequence - 1, head.Digest, [], []); return Task.CompletedTask; }, "Regressed restore head accepted");
    var firstEvent = (await Entries()).OrderBy(e => e.Sequence).First(); var holdId = Guid.NewGuid();
    var twelveMonths = clock.Initial.AddMonths(12);
    await Refused(() => RoleSql(lifecycle, "SELECT security_audit.soft_delete_event($1,$2,$3)", firstEvent.EventId, DateTimeOffset.UtcNow.AddYears(1), lifecycleAuthority), "Future lifecycle soft delete accepted");
    await Refused(() => RoleSql(lifecycle, "SELECT security_audit.soft_delete_event($1,NULL,$2)", firstEvent.EventId, lifecycleAuthority), "Null lifecycle soft delete accepted");
    await Refused(() => RoleSql(lifecycle, "SELECT security_audit.soft_delete_event($1,$2,$3)", firstEvent.EventId, twelveMonths.AddTicks(-10), lifecycleAuthority), "Early soft-delete allowed");
    await RoleSql(lifecycle, "SELECT security_audit.set_hold($1,$2,$3,$4)", firstEvent.EventId, holdId, twelveMonths.AddMonths(1), lifecycleAuthority);
    await Refused(() => RoleSql(lifecycle, "SELECT security_audit.soft_delete_event($1,$2,$3)", firstEvent.EventId, twelveMonths, lifecycleAuthority), "Hold allowed deletion");
    await Refused(() => RoleSql(lifecycle, "SELECT security_audit.release_hold($1,$2,$3)", firstEvent.EventId, Guid.NewGuid(), lifecycleAuthority), "Wrong hold release accepted");
    await RoleSql(lifecycle, "SELECT security_audit.release_hold($1,$2,$3)", firstEvent.EventId, holdId, lifecycleAuthority);
    await RoleSql(lifecycle, "SELECT security_audit.soft_delete_event($1,$2,$3)", firstEvent.EventId, twelveMonths, lifecycleAuthority);
    await using (var filtered = readerRole.CreateCommand("SELECT count(*) FROM security_audit.visible_events WHERE event_id=$1"))
    { filtered.Parameters.AddWithValue(firstEvent.EventId); Check((long)(await filtered.ExecuteScalarAsync())! == 0, "Soft-deleted record absent from ordinary reads"); }
    await RoleSql(lifecycle, "SELECT security_audit.set_hold($1,$2,$3,$4)", firstEvent.EventId, holdId, twelveMonths.AddMonths(1), lifecycleAuthority);
    await Refused(() => RoleSql(lifecycle, "SELECT security_audit.purge_event($1,$2,$3)", firstEvent.EventId, twelveMonths.AddDays(30), lifecycleAuthority), "Held soft-deleted event purge allowed");
    await RoleSql(lifecycle, "SELECT security_audit.release_hold($1,$2,$3)", firstEvent.EventId, holdId, lifecycleAuthority);
    await Refused(() => RoleSql(lifecycle, "SELECT security_audit.purge_event($1,$2,$3)", firstEvent.EventId, DateTimeOffset.UtcNow.AddYears(1), lifecycleAuthority), "Future lifecycle purge accepted");
    await Refused(() => RoleSql(lifecycle, "SELECT security_audit.purge_event($1,NULL,$2)", firstEvent.EventId, lifecycleAuthority), "Null lifecycle purge accepted");
    await RoleSql(lifecycle, "SELECT security_audit.purge_event($1,$2,$3)", firstEvent.EventId, twelveMonths.AddDays(30), lifecycleAuthority);
    Check(await Scalar("SELECT count(*) FROM security_audit.tombstones") == 1, "Authorized purge preserves sequence/digest tombstone");
    AuditIntegrityVerifier.Verify(binding, checkpoint, head.Sequence, head.Digest, await Entries(), await Receipts()); count++;
    // Simulate restoring a stale event over an authoritative deletion tombstone:
    // restore must replay tombstone before any read and cannot resurrect it.
    await Sql("INSERT INTO security_audit.events VALUES($1,$2,$3,$4,$5,$6,$7,$8)", firstEvent.EventId,
        JsonDocument.Parse(firstEvent.CanonicalEvent!).RootElement.GetProperty("operationId").GetGuid(), binding.StreamId, firstEvent.Sequence, clock.Initial, firstEvent.PreviousSha256, firstEvent.EventSha256, firstEvent.CanonicalEvent!);
    await Refused(() => { AuditIntegrityVerifier.Verify(binding, checkpoint, head.Sequence, head.Digest, Entries().GetAwaiter().GetResult(), Receipts().GetAwaiter().GetResult()); return Task.CompletedTask; }, "Restored duplicate deleted event accepted");
    await Sql("DELETE FROM security_audit.events WHERE event_id IN(SELECT event_id FROM security_audit.tombstones)");
    AuditIntegrityVerifier.Verify(binding, checkpoint, head.Sequence, head.Digest, await Entries(), await Receipts()); count++;
    Console.WriteLine($"PASS: {count} real PostgreSQL atomic audit/restricted-role/receipt/head-wait/lifecycle/witness checks.");
    Console.WriteLine("NOT VERIFIED: live SQL roles, independently retained witness, production audit outage preservation, actual retention/backup execution or Azure/provider activation.");
}
finally { Directory.Delete(dir, true); }
sealed class Clock : TimeProvider
{
    public DateTimeOffset Initial { get; } = DateTimeOffset.UtcNow.AddYears(-2);
    private TimeSpan elapsed;
    public override DateTimeOffset GetUtcNow() => Initial + elapsed;
    public void Advance(TimeSpan value) => elapsed += value;
}
sealed class Admission : ITransactionalSessionAdmissionPolicy
{
    public ValueTask<bool> IsEligibleAsync(SessionSubject subject, CancellationToken cancellationToken) => throw new InvalidOperationException("Audited callers cannot use legacy authority.");
    public ValueTask<bool> IsEligibleAsync(SessionSubject subject, NpgsqlConnection connection, NpgsqlTransaction transaction, DateTimeOffset authenticated, CancellationToken ct) => ValueTask.FromResult(true);
}
