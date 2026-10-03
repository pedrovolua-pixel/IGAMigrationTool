using System.Text.Json;
using System.Text.Json.Nodes;
using IdentityAuthority;
using IdentityPolicy;
using IdentitySessions;
using IgaMigration.BffFoundation;
using Microsoft.AspNetCore.DataProtection;
using Npgsql;

if (args is not ["--reset-synthetic-schema"])
    throw new InvalidOperationException("Explicit dedicated synthetic reset argument required.");
await using var f = new Fixture(Environment.GetEnvironmentVariable("IGA_COMPOSITION_TEST_CONNECTION")
    ?? throw new InvalidOperationException("IGA_COMPOSITION_TEST_CONNECTION required."));
await f.Install();
var count = 0;
void Check(bool value, string label) { if (!value) throw new Exception(label); count++; }
async Task Refused(Func<Task> operation, string label)
{
    var refused = false;
    try { await operation(); }
    catch (Exception e) when (e is InvalidOperationException or PostgresException or JsonException) { refused = true; }
    Check(refused, label);
}
async Task<(long Tickets, long Events, long Receipts, long Mutations)> Counts() => (
    await f.Number("SELECT count(*) FROM identity_sessions.tickets"), await f.Number("SELECT count(*) FROM security_audit.events"),
    await f.Number("SELECT count(*) FROM security_audit.operation_receipts"), await f.Number("SELECT count(*) FROM identity_authority.mutations"));

var keys = Path.Combine(Path.GetTempPath(), "iga-composition-keys-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(keys);
try
{
    var protection = DataProtectionProvider.Create(new DirectoryInfo(keys), c => c.SetApplicationName("IndependentComposition"));
    var first = new PostgreSqlTicketStore(f.Runtime, protection, f.Clock, f.RuntimeAuthority, f.Audit);
    var second = new PostgreSqlTicketStore(f.Runtime, protection, f.Clock, f.RuntimeAuthority, f.Audit);
    Check(first.HasAtomicAudit, "Strict constructor must select atomic audited path.");
    foreach (var source in new[] { f.Administrator, f.Publisher, f.Runtime })
    {
        await using var identity = source.CreateCommand("SELECT current_user=session_user,NOT rolsuper AND NOT rolbypassrls AND NOT rolcreaterole AND NOT rolcreatedb FROM pg_roles WHERE rolname=current_user");
        await using var r = await identity.ExecuteReaderAsync();
        Check(await r.ReadAsync() && r.GetBoolean(0) && r.GetBoolean(1), "Operations must connect as actual restricted login, not SET ROLE.");
    }
    Check(await f.Number("SELECT count(*) FROM pg_roles WHERE rolname='icom_owner' AND NOT rolcanlogin AND NOT rolsuper") == 1,
        "Distinct function owner must be NONLOGIN and nonsuperuser.");
    Check(f.Clock.GetUtcNow().UtcTicks % 10 == 0, "Frozen fixture clock starts at PostgreSQL microsecond precision.");
    // Exercise every possible submicrosecond remainder through the real JSON
    // provider publication path. Future rounding remains denied by production.
    for (var remainder = 1; remainder < 10; remainder++)
    {
        f.Clock.Advance(TimeSpan.FromTicks(remainder));
        var precisionSubject = await f.Enroll();
        await using var precision = f.Operator.CreateCommand("SELECT provider_checked_at FROM identity_sessions.subjects WHERE tenant_id=$1 AND object_id=$2");
        precision.Parameters.AddWithValue(precisionSubject.TenantId); precision.Parameters.AddWithValue(precisionSubject.ObjectId);
        var stored = new DateTimeOffset((DateTime)(await precision.ExecuteScalarAsync())!);
        var now = f.Clock.GetUtcNow();
        Check(stored.UtcTicks % 10 == 0 && Math.Abs(stored.UtcTicks - now.UtcTicks) < 10,
            "Actual provider JSON timestamp cast quantizes submicrosecond input to PostgreSQL precision.");
        if (remainder == 9)
            Check(stored > now, "Nine-tick reproduction yields a future provider timestamp.");
        var precisionVersion = await f.Version(precisionSubject);
        if (stored > now)
            await Refused(() => first.StoreAsync(f.Ticket(precisionSubject, precisionVersion)), "Rounded future provider time remains fail-closed.");
        f.Clock.Advance(TimeSpan.FromTicks(10 - remainder));
        // A fresh aligned subject avoids moving an existing observation back in
        // time. No sleep/retry or relaxed production time comparison is used.
        var alignedSubject = await f.Enroll();
        var precisionKey = await first.StoreAsync(f.Ticket(alignedSubject, await f.Version(alignedSubject)));
        Check(await second.RetrieveAsync(precisionKey) is not null,
            "Microsecond-aligned observation admits across real store instances.");
        await first.RemoveAsync(precisionKey);
    }
    var a = await f.Enroll(); var other = await f.Enroll();
    var v = await f.Version(a); var before = await Counts();
    var ticket = f.Ticket(a, v);
    // A bounded completion detects the previous separate-connection self-lock.
    var key = await first.StoreAsync(ticket).WaitAsync(TimeSpan.FromSeconds(5));
    var loaded = await second.RetrieveAsync(key).WaitAsync(TimeSpan.FromSeconds(5));
    Check(loaded is not null, "Real authority and audited store must retrieve across instances without self-lock.");
    var after = await Counts();
    Check(after == (before.Tickets + 1, before.Events + 1, before.Receipts + 1, before.Mutations), "Issue ticket/event/receipt commit exactly once.");
    await second.RenewAsync(key, loaded!);
    Check(await first.RetrieveAsync(key) is not null, "Renewal uses real same-transaction admission.");
    Check((await Counts()).Events == after.Events, "Ordinary renewal emits no new issuance event.");
    var rotated = await second.RotateAsync(key, f.Ticket(a, v));
    Check(rotated is not null && await first.RetrieveAsync(key) is null && await first.RetrieveAsync(rotated) is not null,
        "Rotation replaces exact target atomically through real authority.");
    Check((await Counts()).Events == after.Events + 1, "Rotation produces one event.");
    var otherKey = await first.StoreAsync(f.Ticket(other, await f.Version(other)));
    await first.RemoveAsync(rotated!); await second.RemoveAsync(rotated!);
    Check(await first.RetrieveAsync(rotated!) is null && await second.RetrieveAsync(otherKey) is not null, "Local revoke preserves another subject.");
    var snapshot = (await f.RuntimeAuthority.ReadAsync(a))!;
    var bridge = new PostgreSqlBffSubjectAuthority(f.RuntimeAuthority, f.Clock);
    var admission = await bridge.CheckAsync(new(a.TenantId, a.ObjectId), CancellationToken.None);
    Check(admission is
    {
        Active: true, Assigned: true, ProviderStatusValid: true, IsGuest: false, OrganizationalGuestOriginVerified: false,
        OrganizationalGuestHomeTenantId: null
    } && admission.SecurityVersion == v && admission.CoarseRoles.SequenceEqual(["PilotConsultant"])
        && admission.SignInValidFromUtc == DateTimeOffset.UnixEpoch, "Opt-in BFF bridge projects only current internal admission and exact coarse role.");
    Check(await bridge.CheckAsync(new(Guid.NewGuid(), a.ObjectId), CancellationToken.None) is null, "BFF bridge rejects cross-tenant subject substitution.");
    Check(f.RuntimeAuthority.ExactAssignments(snapshot, f.Scope, "normalized", f.Clock.GetUtcNow()).Length == 1,
        "Exact complete scope/category projects assignment.");
    foreach (var wrong in new[] { f.Scope with { CustomerId = Guid.NewGuid() }, f.Scope with { ProjectId = Guid.NewGuid() },
        f.Scope with { EnvironmentId = Guid.NewGuid() }, f.Scope with { AssessmentId = Guid.NewGuid() } })
        Check(f.RuntimeAuthority.ExactAssignments(snapshot, wrong, "normalized", f.Clock.GetUtcNow()).IsEmpty, "Every scope component is exact.");
    Check(f.RuntimeAuthority.ExactAssignments(snapshot, f.Scope, "protected", f.Clock.GetUtcNow()).IsEmpty, "Category must not widen assignment.");

    // Real provider publication keeps current resource cutoff authoritative over
    // both caller ticket properties and original persisted authentication time.
    var original = f.Clock.GetUtcNow().AddMinutes(-2);
    var cutoffSubject = await f.Enroll();
    var cutoffKey = await first.StoreAsync(f.Ticket(cutoffSubject, await f.Version(cutoffSubject), original));
    await f.Publish(cutoffSubject, f.Clock.GetUtcNow().AddMinutes(-1));
    Check(await second.RetrieveAsync(cutoffKey) is null, "Resource cutoff invalidates original authentication.");
    Check((await bridge.CheckAsync(new(cutoffSubject.TenantId, cutoffSubject.ObjectId), CancellationToken.None))!.SignInValidFromUtc
        == f.Clock.GetUtcNow().AddMinutes(-1), "Internal BFF admission projects current resource cutoff.");
    var cutoffVersion = await f.Version(cutoffSubject);
    await Refused(() => first.StoreAsync(f.Ticket(cutoffSubject, cutoffVersion, original)), "New ticket cannot bypass resource cutoff with old authentication.");

    // Two independent retries after a committed result is deliberately discarded
    // reconcile only the immutable receipt; no credential is replayed.
    var command = f.Command(a, snapshot.Enrollment.Revision, AuthorityOperation.SuspendSubject);
    AuthorityReceiptV1? committed = null;
    async Task CommitThenLoseAcknowledgment()
    {
        committed = await f.AdminAuthority.ExecuteAsync(command);
        throw new TimeoutException("Synthetic acknowledgment loss after actual COMMIT.");
    }
    var acknowledgmentLost = false;
    try { await CommitThenLoseAcknowledgment(); }
    catch (TimeoutException) { acknowledgmentLost = true; }
    Check(acknowledgmentLost && committed is not null, "Fixture loses acknowledgment after actual PostgreSQL commit.");
    var knownCommit = committed ?? throw new InvalidOperationException("Committed fixture result absent.");
    var committedCounts = await Counts();
    await using (var connection = await f.Administrator.OpenConnectionAsync())
    await using (var transaction = await connection.BeginTransactionAsync())
    {
        var request = new OperationReceiptRequestV1(command.Decision.CommandId, f.Actor, SecurityAuditActorKind.Human,
            f.Actor, SecurityAuditAction.AuthorityChanged, a, command.Decision.PayloadSha256);
        var reconciled = await f.Audit.ResolveReceiptAsync(connection, transaction, request);
        Check(reconciled is { NewSessionReference: null } && reconciled.SecurityVersion == knownCommit.SecurityVersion
            && reconciled.EventIds.SequenceEqual(knownCommit.EventIds), "Internal acknowledgment reconciliation returns exact metadata without cookie/key/ticket.");
    }
    var repeated = await f.AdminAuthority.ExecuteAsync(command);
    Check(repeated.OperationId == knownCommit.OperationId && repeated.SecurityVersion == knownCommit.SecurityVersion
        && repeated.EventIds.SequenceEqual(knownCommit.EventIds), "Receipt resolves committed command before stale expected revision.");
    Check(await Counts() == committedCounts, "Receipt retry cannot change state, append event or duplicate receipt.");
    var conflict = command with { Decision = command.Decision with { Reason = AuthorityReason.Incident } };
    conflict = conflict with { Decision = conflict.Decision with { PayloadSha256 = AuthorityCodec.PayloadDigest(conflict) } };
    await Refused(() => f.AdminAuthority.ExecuteAsync(conflict), "Reused operation with different digest denied.");
    Check(await Counts() == committedCounts, "Conflicting receipt cannot change data.");
    await Refused(() => first.StoreAsync(f.Ticket(a, v)), "Suspended subject cannot issue old-version session.");
    Check(await bridge.CheckAsync(new(a.TenantId, a.ObjectId), CancellationToken.None) is null, "BFF bridge denies suspended current authority.");
    Check(await second.RetrieveAsync(otherKey) is not null, "Authority mutation preserves other subject.");
    var concurrentSubject = await f.Enroll();
    var concurrentSnapshot = (await f.RuntimeAuthority.ReadAsync(concurrentSubject))!;
    var same = f.Command(concurrentSubject, concurrentSnapshot.Enrollment.Revision, AuthorityOperation.SuspendSubject);
    var raceBefore = await Counts();
    var receipts = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => f.AdminAuthority.ExecuteAsync(same))).WaitAsync(TimeSpan.FromSeconds(8));
    Check(receipts.All(r => r.SecurityVersion == concurrentSnapshot.SecurityVersion + 1 && r.EventIds.SequenceEqual(receipts[0].EventIds)), "Concurrent exact command retries advance once.");
    var raceAfter = await Counts();
    Check(raceAfter.Events == raceBefore.Events + 1 && raceAfter.Receipts == raceBefore.Receipts + 1 && raceAfter.Mutations == raceBefore.Mutations + 1,
        "Concurrent retries commit exactly one authority/event/receipt.");
    var newSubject = new SessionSubject(f.Actor.TenantId, Guid.NewGuid());
    var newEnrollment = new SubjectEnrollmentV1("subject-enrollment-v1", newSubject, 1, EnrollmentLifecycle.Pending,
        OrganizationalOrigin.InternalOrganizational, f.Actor.TenantId, f.Clock.GetUtcNow(), f.Attribution());
    var newFirst = f.Command(newSubject, 0, AuthorityOperation.EnrollPending, newEnrollment);
    var newSecond = f.Command(newSubject, 0, AuthorityOperation.EnrollPending, newEnrollment);
    async Task<bool> TryEnrollment(AuthorityCommandV1 value)
    {
        try { await f.AdminAuthority.ExecuteAsync(value); return true; }
        catch (Exception e) when (e is InvalidOperationException || e is PostgresException { SqlState: "P0001" or "23505" }) { return false; }
    }
    var enrollmentBefore = await Counts();
    var enrollmentWins = await Task.WhenAll(TryEnrollment(newFirst), TryEnrollment(newSecond)).WaitAsync(TimeSpan.FromSeconds(8));
    Check(enrollmentWins.Count(w => w) == 1, "Different concurrent commands cannot overwrite nonexistent enrollment.");
    Check((await Counts()).Mutations == enrollmentBefore.Mutations + 1 && (await Counts()).Events == enrollmentBefore.Events + 1,
        "New-enrollment race commits one attributed event and mutation.");
    string CommandJsonWithDigest(JsonNode node)
    {
        node["decision"]!["payloadSha256"] = new string('0', 64);
        using var document = JsonDocument.Parse(node.ToJsonString()); using var bytes = new MemoryStream();
        void Canonical(Utf8JsonWriter writer, JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                writer.WriteStartObject();
                foreach (var field in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                { writer.WritePropertyName(field.Name); Canonical(writer, field.Value); }
                writer.WriteEndObject();
            }
            else if (value.ValueKind == JsonValueKind.Array)
            { writer.WriteStartArray(); foreach (var child in value.EnumerateArray()) Canonical(writer, child); writer.WriteEndArray(); }
            else value.WriteTo(writer);
        }
        using (var writer = new Utf8JsonWriter(bytes)) Canonical(writer, document.RootElement);
        node["decision"]!["payloadSha256"] = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes.ToArray()));
        return node.ToJsonString();
    }
    async Task SchemaError(NpgsqlDataSource source, string sql, string json, string expectedState, string? expectedMessage = null)
    {
        await using var connection = await source.OpenConnectionAsync(); await using var transaction = await connection.BeginTransactionAsync();
        var immediate = false;
        try
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Jsonb, json); await command.ExecuteNonQueryAsync();
        }
        catch (PostgresException e) { immediate = e.SqlState == expectedState && (expectedMessage is null || e.MessageText == expectedMessage); }
        await transaction.RollbackAsync();
        Check(immediate, "Direct SQL must reject malformed counter/digest at schema guard before deferred receipt checks.");
    }
    var malformedSubject = new SessionSubject(f.Actor.TenantId, Guid.NewGuid());
    var numericBase = f.Command(malformedSubject, 0, AuthorityOperation.EnrollPending, newEnrollment with { Subject = malformedSubject });
    var numericBaseline = await Counts();
    foreach (var field in new[] { "expectedRevision", "revision" })
    {
        var node = JsonNode.Parse(AuthorityCodec.Serialize(numericBase))!;
        node[field == "expectedRevision" ? "decision" : "enrollment"]![field] = JsonValue.Create(field == "expectedRevision" ? 0.4m : 1.4m);
        await SchemaError(f.Administrator, "SELECT * FROM identity_authority.apply_command($1)", CommandJsonWithDigest(node), "P0001", "authority integer denied");
    }
    var overflow = JsonNode.Parse(AuthorityCodec.Serialize(numericBase))!;
    overflow["decision"]!["expectedRevision"] = JsonNode.Parse("9223372036854775808");
    await SchemaError(f.Administrator, "SELECT * FROM identity_authority.apply_command($1)", CommandJsonWithDigest(overflow), "22003");
    var forged = JsonNode.Parse(AuthorityCodec.Serialize(numericBase))!; forged["decision"]!["payloadSha256"] = new string('f', 64);
    await SchemaError(f.Administrator, "SELECT * FROM identity_authority.apply_command($1)", forged.ToJsonString(), "P0001", "authority payload digest denied");
    var wrongProviderDigest = await f.Observation(other);
    await SchemaError(f.Publisher, "SELECT * FROM identity_authority.publish_provider($1,NULL,'11111111-1111-1111-1111-111111111111','ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff')",
        AuthorityCodec.Serialize(wrongProviderDigest.Observation), "P0001", "provider payload digest denied");
    Check(await Counts() == numericBaseline, "Immediate numeric/digest denial preserves all authority/session/audit counts.");
    var lockSubject = await f.Enroll(); var lockSnapshot = (await f.RuntimeAuthority.ReadAsync(lockSubject))!;
    async Task<string?> RacingIssue()
    {
        try { return await first.StoreAsync(f.Ticket(lockSubject, lockSnapshot.SecurityVersion)); }
        catch (InvalidOperationException) { return null; }
    }
    var issuances = Enumerable.Range(0, 8).Select(_ => RacingIssue()).ToArray();
    var suspending = f.AdminAuthority.ExecuteAsync(f.Command(lockSubject, lockSnapshot.Enrollment.Revision, AuthorityOperation.SuspendSubject));
    await Task.WhenAll(issuances.Cast<Task>().Append(suspending)).WaitAsync(TimeSpan.FromSeconds(8));
    var issuedTickets = await Task.WhenAll(issuances.Select(t => t.Result).OfType<string>().Select(issued => second.RetrieveAsync(issued)));
    Check(issuedTickets.All(t => t is null), "Concurrent authority change invalidates every issued old-version session.");
    Check((await f.RuntimeAuthority.ReadAsync(lockSubject))!.SecurityVersion == lockSnapshot.SecurityVersion + 1,
        "Opposing authority and session operations complete without lock-order deadlock.");

    // Failure is a real permission refusal in the same PostgreSQL transaction.
    var rollbackSubject = await f.Enroll();
    var rollbackSnapshot = (await f.RuntimeAuthority.ReadAsync(rollbackSubject))!;
    var rollbackKey = await first.StoreAsync(f.Ticket(rollbackSubject, rollbackSnapshot.SecurityVersion));
    var baseline = await Counts();
    await Fixture.Sql(f.Operator, "REVOKE EXECUTE ON FUNCTION security_audit.append_event(uuid,uuid,text,text) FROM icom_admin");
    await Refused(() => f.AdminAuthority.ExecuteAsync(f.Command(rollbackSubject, rollbackSnapshot.Enrollment.Revision, AuthorityOperation.SuspendSubject)), "Audit outage denies authority mutation.");
    Check(await Counts() == baseline && (await f.RuntimeAuthority.ReadAsync(rollbackSubject))!.SecurityVersion == rollbackSnapshot.SecurityVersion
        && await first.RetrieveAsync(rollbackKey) is not null, "Audit failure rolls back authority, revoked ticket, event and receipt.");
    await Fixture.Sql(f.Operator, "GRANT EXECUTE ON FUNCTION security_audit.append_event(uuid,uuid,text,text) TO icom_admin");
    await Fixture.Sql(f.Operator, "REVOKE EXECUTE ON FUNCTION security_audit.append_event(uuid,uuid,text,text) FROM icom_runtime");
    await Refused(() => first.RemoveAsync(rollbackKey), "Audit outage denies successful signout store removal.");
    await Refused(() => first.RotateAsync(rollbackKey, f.Ticket(rollbackSubject, rollbackSnapshot.SecurityVersion)), "Audit outage denies rotation.");
    Check(await Counts() == baseline && await second.RetrieveAsync(rollbackKey) is not null, "Failed revoke/rotation preserve old session and counts.");
    await Fixture.Sql(f.Operator, "GRANT EXECUTE ON FUNCTION security_audit.append_event(uuid,uuid,text,text) TO icom_runtime");

    foreach (var sql in new[] { "UPDATE identity_sessions.subjects SET active=true", "DELETE FROM identity_sessions.tickets",
        "SELECT key_hash,protected_ticket FROM identity_sessions.tickets", "SELECT key_hash FROM identity_sessions.tickets",
        "UPDATE security_audit.events SET canonical_event='{}'", "DELETE FROM security_audit.operation_receipts",
        "DELETE FROM identity_authority.writer_bindings", "SELECT security_audit.append_event_checked_binding(NULL,NULL,'{}','x')",
        "SELECT identity_authority.apply_command('{}')", "SELECT identity_authority.publish_provider('{}',NULL,NULL,'x')" })
        await Refused(() => Fixture.Sql(f.Runtime, sql), "Runtime bypass refused: " + sql);
    await Refused(() => Fixture.Sql(f.Publisher, "SELECT identity_authority.apply_command('{}')"), "Provider cannot administer subjects.");
    await Refused(() => Fixture.Sql(f.Administrator, "SELECT identity_authority.publish_provider('{}',NULL,NULL,'x')"), "Administrator cannot publish provider state.");
    await Refused(async () => await Fixture.Sql(f.Runtime, "SELECT security_audit.issue_ticket($1,$2,$3,$4,$5,$6,$7,$8,NULL)", new string('b', 64), Guid.NewGuid(),
        other.TenantId, other.ObjectId, await f.Version(other), f.Clock.GetUtcNow(), f.Clock.GetUtcNow().AddHours(1), new byte[16]), "NULL operation cannot bypass issue audit.");
    var unmatchedBefore = await Counts();
    await Refused(async () =>
    {
        await using var connection = await f.Runtime.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await f.Audit.AppendAsync(connection, transaction, new(Guid.NewGuid(), Guid.NewGuid(), f.Clock.GetUtcNow(),
            SecurityAuditActorKind.Workload, f.Actor, Guid.NewGuid(), SecurityAuditAction.SessionIssued,
            SecurityAuditOutcome.Succeeded, SecurityAuditReason.None, other, Guid.NewGuid(), null, await f.Version(other)));
        await transaction.CommitAsync();
    }, "Standalone successful mutation event cannot commit without receipt.");
    Check(await Counts() == unmatchedBefore, "Deferred event/receipt refusal rolls back stream head and event.");
    var alienStream = Guid.NewGuid(); var alienBinding = Guid.NewGuid();
    await Fixture.Sql(f.Operator, "INSERT INTO security_audit.streams(stream_id,environment_id,writer_binding_reference,writer_tenant_id,writer_object_id,application_client_id) VALUES($1,'another-environment',$2,$3,$4,$5)",
        alienStream, alienBinding, f.Actor.TenantId, Guid.NewGuid(), f.Roles.ClientId);
    await Refused(() => Fixture.Sql(f.Runtime, "SELECT security_audit.lock_head($1,'another-environment',$2)", alienStream, alienBinding),
        "Known second stream binding cannot impersonate authorized writer role.");
    string ReverseObject(string json)
    {
        using var value = JsonDocument.Parse(json); using var bytes = new MemoryStream();
        using (var writer = new Utf8JsonWriter(bytes))
        {
            writer.WriteStartObject();
            foreach (var field in value.RootElement.EnumerateObject().Reverse())
            { writer.WritePropertyName(field.Name); field.Value.WriteTo(writer); }
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(bytes.ToArray());
    }
    var canonicalBefore = await Counts();
    long nextSequence; string previousDigest;
    await using (var query = f.Operator.CreateCommand("SELECT head_sequence,head_sha256 FROM security_audit.streams WHERE stream_id=$1"))
    {
        query.Parameters.AddWithValue(f.Audit.Binding.StreamId); await using var row = await query.ExecuteReaderAsync(); await row.ReadAsync();
        nextSequence = row.GetInt64(0) + 1; previousDigest = row.GetString(1);
    }
    var deniedEvent = new SecurityAuditEventV1(Guid.NewGuid(), Guid.NewGuid(), f.Clock.GetUtcNow(), SecurityAuditActorKind.Anonymous,
        null, Guid.NewGuid(), SecurityAuditAction.AuthenticationDenied, SecurityAuditOutcome.Denied, SecurityAuditReason.AuthorityDenied, null, null, null, null);
    var exactEvent = SecurityAuditCanonical.Event(f.Audit.Binding, deniedEvent, nextSequence, previousDigest);
    using (var parsed = JsonDocument.Parse(exactEvent))
        foreach (var variant in new[] { JsonSerializer.Serialize(parsed.RootElement, new JsonSerializerOptions { WriteIndented = true }),
            ReverseObject(exactEvent), exactEvent.Replace("security-audit-session-v1", "security-audit-session-v\\u0031", StringComparison.Ordinal) })
            await Refused(() => Fixture.Sql(f.Runtime, "SELECT security_audit.append_event($1,$2,$3,$4)", f.Audit.Binding.StreamId,
                f.Audit.Binding.WriterBindingReference, variant, SecurityAuditCanonical.Hash(variant)), "Direct SQL refuses equivalent noncanonical event bytes even with correct digest.");
    Check(await Counts() == canonicalBefore, "Noncanonical event rejection leaves head/event/receipt counts unchanged.");
    foreach (var alterRequest in new[] { true, false })
        foreach (var alter in new Func<string, string>[] { ReverseObject, json => " " + json,
            json => json.Replace("operation-receipt", "operation-receip\\u0074", StringComparison.Ordinal) })
            await Refused(async () =>
            {
                await using var connection = await f.Runtime.OpenConnectionAsync(); await using var transaction = await connection.BeginTransactionAsync();
                var operation = Guid.NewGuid(); var reference = Guid.NewGuid();
                var written = await f.Audit.AppendAsync(connection, transaction, new(Guid.NewGuid(), operation, f.Clock.GetUtcNow(),
                    SecurityAuditActorKind.Workload, f.Actor, Guid.NewGuid(), SecurityAuditAction.SessionRevoked, SecurityAuditOutcome.Succeeded,
                    SecurityAuditReason.None, other, reference, null, await f.Version(other)));
                var request = new OperationReceiptRequestV1(operation, f.Actor, SecurityAuditActorKind.Workload, f.Actor,
                    SecurityAuditAction.SessionRevoked, other, SecurityAuditCanonical.Hash("closed-metadata-only"), OldSessionReference: reference);
                var receipt = new OperationReceiptV1(request, SecurityAuditOutcome.Succeeded, f.Clock.GetUtcNow(), null, await f.Version(other), null, [written.EventId]);
                var requestText = SecurityAuditCanonical.Request(request); var receiptText = SecurityAuditCanonical.Receipt(receipt);
                await using var append = new NpgsqlCommand("SELECT security_audit.append_receipt($1,$2,$3,$4)", connection, transaction);
                append.Parameters.AddWithValue(operation); append.Parameters.AddWithValue(alterRequest ? alter(requestText) : requestText);
                append.Parameters.AddWithValue(alterRequest ? receiptText : alter(receiptText)); append.Parameters.AddWithValue(new[] { written.EventId });
                await append.ExecuteNonQueryAsync(); await transaction.CommitAsync();
            }, "Direct SQL refuses noncanonical request/receipt representation.");
    Check(await Counts() == canonicalBefore, "Noncanonical receipt rejection rolls back matching event and head.");

    // External Member still requires external lifecycle and current exact home
    // proof. Home cutoff is checked against original session authentication.
    var external = await f.Enroll(true, other);
    var externalSnapshot = (await f.RuntimeAuthority.ReadAsync(external))!;
    Check(externalSnapshot.Provider!.UserType == ProviderUserType.Member && f.RuntimeAuthority.Eligible(externalSnapshot, f.Clock.GetUtcNow()), "External Member follows reviewed external origin.");
    var externalKey = await first.StoreAsync(f.Ticket(external, externalSnapshot.SecurityVersion, original));
    await f.Publish(external, homeCutoff: f.Clock.GetUtcNow().AddMinutes(-1));
    Check(await second.RetrieveAsync(externalKey) is null, "Home cutoff denies original session even with valid resource cutoff.");
    var externalAdmission = await bridge.CheckAsync(new(external.TenantId, external.ObjectId), CancellationToken.None);
    Check(externalAdmission is { IsGuest: true, GuestOnboardingValid: true, OrganizationalGuestOriginVerified: true }
        && externalAdmission.OrganizationalGuestHomeTenantId == externalSnapshot.Enrollment.HomeTenantId
        && externalAdmission.SignInValidFromUtc == f.Clock.GetUtcNow().AddMinutes(-1), "External Member BFF projection requires reviewed external lifecycle and max home/resource cutoff.");
    var regressingHome = await f.Observation(external, homeCutoff: DateTimeOffset.UnixEpoch);
    await Refused(() => f.ProviderAuthority.PublishAsync(regressingHome, Guid.NewGuid()), "Home cutoff cannot regress and resurrect original authentication.");
    Check(await second.RetrieveAsync(externalKey) is null, "Rejected home regression preserves session denial.");
    var missingHome = await f.Observation(external);
    await Refused(() => f.ProviderAuthority.PublishAsync(missingHome with { HomeStatus = null }, Guid.NewGuid()), "External Member cannot publish without home proof.");

    // Verify chain independently from the implementation's verifier, using raw
    // canonical UTF8 digests and one receipt for every successful mutation event.
    await using (var read = f.Operator.CreateCommand("SELECT sequence,previous_sha256,event_sha256,canonical_event,operation_id,event_id FROM security_audit.events ORDER BY sequence"))
    await using (var rows = await read.ExecuteReaderAsync())
    {
        long sequence = 0; var prior = SecurityAuditCanonical.EmptyDigest;
        while (await rows.ReadAsync())
        {
            var canonical = rows.GetString(3);
            var digest = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical)));
            Check(rows.GetInt64(0) == ++sequence && rows.GetString(1) == prior && rows.GetString(2) == digest, "Independent contiguous digest chain.");
            using var json = JsonDocument.Parse(canonical); var value = json.RootElement;
            Check(value.EnumerateObject().Count() == 25 && value.GetProperty("authenticationEvidence").GetString() == "None"
                && value.GetProperty("writerBindingReference").GetGuid() == f.Audit.Binding.WriterBindingReference, "Closed event binding and minimized evidence.");
            prior = digest;
        }
    }
    Check(await f.Number("SELECT count(*) FROM security_audit.events e LEFT JOIN security_audit.operation_receipts r USING(operation_id) WHERE r.operation_id IS NULL OR NOT e.event_id=ANY(r.event_ids)") == 0,
        "Every successful mutation event has matching durable receipt.");
    await using (var inspect = f.Operator.CreateCommand("SELECT receipt_canonical FROM security_audit.operation_receipts"))
    await using (var rows = await inspect.ExecuteReaderAsync())
        while (await rows.ReadAsync())
        {
            var receipt = rows.GetString(0);
            Check(!receipt.Contains("protected_ticket", StringComparison.OrdinalIgnoreCase) && !receipt.Contains("key_hash", StringComparison.OrdinalIgnoreCase)
                && !receipt.Contains("AuthenticatedUtc", StringComparison.OrdinalIgnoreCase) && !receipt.Contains(key, StringComparison.Ordinal), "Receipt contains no replay credential or authentication payload.");
            var parsed = SecurityAuditCanonical.ReadReceipt(receipt);
            Check(parsed.Request.Issuer == f.Actor && parsed.EventIds.Count == 1, "Receipt retains exact nonsecret issuer and event metadata.");
        }

    // Use a genuinely near-expiry provider timestamp and a short actual wait.
    // The advanced synthetic clock stays behind wall time, so PostgreSQL's
    // future-event guard cannot be the reason this admission is refused.
    f.Clock.SampleWallClock();
    Check(f.Clock.GetUtcNow().UtcTicks % 10 == 0, "Resampled fixture wall clock retains PostgreSQL precision.");
    var expiring = await f.Enroll(publish: false);
    var nearExpiry = await f.Observation(expiring);
    nearExpiry = nearExpiry with { Observation = nearExpiry.Observation with { StartedAtUtc = f.Clock.GetUtcNow().AddMinutes(-15).AddSeconds(2) } };
    await f.ProviderAuthority.PublishAsync(nearExpiry, Guid.NewGuid());
    var expireBefore = await Counts();
    await using (var connection = await f.Operator.OpenConnectionAsync())
    await using (var transaction = await connection.BeginTransactionAsync())
    {
        await using var head = new NpgsqlCommand("SELECT stream_id FROM security_audit.streams WHERE stream_id=$1 FOR UPDATE", connection, transaction);
        head.Parameters.AddWithValue(f.Audit.Binding.StreamId); await head.ExecuteScalarAsync();
        var waiting = first.StoreAsync(f.Ticket(expiring, await f.Version(expiring)));
        var observed = false;
        for (var attempt = 0; attempt < 100 && !observed; attempt++)
        {
            await using var probe = f.Operator.CreateCommand("SELECT EXISTS(SELECT 1 FROM pg_stat_activity WHERE datname=current_database() AND usename='icom_runtime' AND wait_event_type='Lock' AND query LIKE '%lock_head%')");
            observed = (bool)(await probe.ExecuteScalarAsync())!; if (!observed) await Task.Delay(10);
        }
        Check(observed && !waiting.IsCompleted, "Real runtime waits on audit stream head.");
        await Task.Delay(2100);
        f.Clock.Advance(TimeSpan.FromSeconds(2)); await transaction.CommitAsync();
        await Refused(async () => await waiting, "Provider expiry after head lock denies issuance.");
        Check(await Counts() == expireBefore, "Post-lock expiry rolls back ticket/event/receipt.");
    }
    Console.WriteLine($"PASS: {count} independently authored real PostgreSQL authority/audit/session composition checks.");
    Console.WriteLine("NOT VERIFIED: real COMMIT network interruption; live Entra/Graph/Azure roles, keys or proxy; independently retained audit witness; outage preservation; retention/backup operation; production release.");
}
finally { Directory.Delete(keys, true); }
