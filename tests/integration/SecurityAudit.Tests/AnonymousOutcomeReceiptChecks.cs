using System.Globalization;
using System.Text;
using System.Text.Json;
using IdentitySessions;
using Npgsql;

// Actual local PostgreSQL model only: no Blob delivery, independent witness or production receipt API.
internal static class AnonymousOutcomeReceiptChecks
{
    private sealed record Source(Guid JournalBinding, Guid StorageReference);
    private sealed record Outcome(Guid EventId, long Sequence, string Digest);
    private sealed class FixtureClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    public static async Task<int> RunAsync(NpgsqlDataSource owner, string supplied)
    {
        var count = 0;
        void Check(bool value, string label) { if (!value) throw new Exception("AP-PG: " + label); count++; }
        async Task Sql(NpgsqlDataSource source, string sql, params object[] values)
        {
            await using var c = source.CreateCommand(sql);
            foreach (var value in values) c.Parameters.AddWithValue(value);
            await c.ExecuteNonQueryAsync();
        }
        async Task<long> Scalar(string sql)
        { await using var c = owner.CreateCommand(sql); return Convert.ToInt64(await c.ExecuteScalarAsync()); }
        async Task Refused(Func<Task> task, string label)
        {
            try { await task(); throw new Exception("AP-PG refusal missing: " + label); }
            catch (PostgresException e) when (e.SqlState is "P0001" or "42501" or "23503" or "23505" or "23514") { count++; }
            catch (InvalidOperationException e) when (e.Message == "Authentication failure journal denied.") { count++; }
        }
        var binding = new SecurityAuditBindingV1(Guid.NewGuid(), "synthetic-anonymous", Guid.NewGuid(),
            new(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid());
        var journal = Guid.NewGuid(); var otherJournal = Guid.NewGuid();
        await Sql(owner, FixtureSql);
        await Sql(owner, "INSERT INTO security_audit.streams(stream_id,environment_id,writer_binding_reference,writer_tenant_id,writer_object_id,application_client_id) VALUES($1,$2,$3,$4,$5,$6)",
            binding.StreamId, binding.EnvironmentId, binding.WriterBindingReference, binding.Writer.TenantId, binding.Writer.ObjectId, binding.ApplicationClientId);
        await Sql(owner, "INSERT INTO ap_pg.bindings VALUES($1,$2)", binding.StreamId, AuthenticationFailureJournalCodec.MaximumEncodedLength);
        await Sql(owner, "INSERT INTO ap_pg.journals VALUES($1),($2)", journal, otherJournal);
        await Sql(owner, "INSERT INTO security_audit.writer_roles VALUES($1,'iga_ap_outcome_writer',$2,ARRAY['AuthenticationDenied','AuthenticationFailed']),($1,'iga_ap_reconciler',$2,ARRAY['AuthenticationDenied','AuthenticationFailed'])", binding.StreamId, binding.WriterBindingReference);
        await Sql(owner, "INSERT INTO ap_pg.roles VALUES('iga_ap_outcome_writer',$1,false,false),('iga_ap_reconciler',$1,true,false),('iga_ap_reader',$1,false,true)", binding.StreamId);
        NpgsqlDataSource As(string role) => NpgsqlDataSource.Create(new NpgsqlConnectionStringBuilder(supplied) { Username = role }.ConnectionString);
        await using var writer = As("iga_ap_outcome_writer"); await using var reconciler = As("iga_ap_reconciler"); await using var reader = As("iga_ap_reader");
        var clock = new FixtureClock(new DateTimeOffset(DateTimeOffset.UtcNow.AddDays(-1).Ticks / 10 * 10 + 7, TimeSpan.Zero));
        var audit = new PostgreSqlSecurityAudit(binding, clock);
        AuthenticationFailureJournalV1 Descriptor(SecurityAuditAction action = SecurityAuditAction.AuthenticationDenied) =>
            new AuthenticationFailureAttempt(binding, clock).Observe(SecurityAuditActorKind.Anonymous, null, action,
                action == SecurityAuditAction.AuthenticationDenied ? SecurityAuditOutcome.Denied : SecurityAuditOutcome.Failed,
                SecurityAuditReason.InvalidProtocol);
        string Bytes(AuthenticationFailureJournalV1 d) => Encoding.UTF8.GetString(AuthenticationFailureJournalCodec.Serialize(d, binding, clock.Now));
        async Task<Outcome?> Resolve(NpgsqlConnection c, NpgsqlTransaction t, AuthenticationFailureJournalV1 d, bool readOnly = false)
        {
            await using var q = new NpgsqlCommand("SELECT event_id,sequence,event_digest FROM ap_pg." + (readOnly ? "read_outcome" : "lock_outcome") + "($1,$2,$3)", c, t);
            q.Parameters.AddWithValue(binding.StreamId); q.Parameters.AddWithValue(Bytes(d)); q.Parameters.AddWithValue(AuthenticationFailureJournalCodec.Digest(d, binding, clock.Now));
            await using var r = await q.ExecuteReaderAsync();
            return await r.ReadAsync() ? new(r.GetGuid(0), r.GetInt64(1), r.GetString(2)) : null;
        }
        async Task<Outcome> Persist(NpgsqlDataSource role, AuthenticationFailureJournalV1 d, Source? source = null, bool failAfterOutcome = false)
        {
            _ = AuthenticationFailureJournalCodec.Parse(Encoding.UTF8.GetBytes(Bytes(d)), binding, clock.Now);
            if (d.ActorKind != SecurityAuditActorKind.Anonymous || d.Subject is not null || d.SessionReference is not null || d.SecurityVersion is not null)
                throw new InvalidOperationException("Authentication failure journal denied.");
            await using var c = await role.OpenConnectionAsync(); await using var t = await c.BeginTransactionAsync();
            var result = await Resolve(c, t, d);
            if (result is null)
            {
                var committed = await audit.AppendAsync(c, t, new(d.EventId, d.OperationId, d.OccurredAtUtc,
                    d.ActorKind, null, d.CorrelationId, d.Action, d.Outcome, d.Reason, null, null, null, null));
                await using var insert = new NpgsqlCommand("SELECT ap_pg.record_outcome($1,$2,$3)", c, t);
                insert.Parameters.AddWithValue(binding.StreamId); insert.Parameters.AddWithValue(Bytes(d));
                insert.Parameters.AddWithValue(AuthenticationFailureJournalCodec.Digest(d, binding, clock.Now));
                await insert.ExecuteNonQueryAsync(); result = new(committed.EventId, committed.Sequence, committed.EventSha256);
            }
            if (failAfterOutcome) throw new IOException("Synthetic failure before source linkage/COMMIT.");
            if (source is not null)
            {
                await using var link = new NpgsqlCommand("SELECT ap_pg.record_source($1,$2,$3,$4,$5)", c, t);
                link.Parameters.AddWithValue(binding.StreamId); link.Parameters.AddWithValue(Bytes(d));
                link.Parameters.AddWithValue(AuthenticationFailureJournalCodec.Digest(d, binding, clock.Now));
                link.Parameters.AddWithValue(source.JournalBinding); link.Parameters.AddWithValue(source.StorageReference);
                await link.ExecuteNonQueryAsync();
            }
            await t.CommitAsync(); return result;
        }
        async Task<Outcome?> Read(AuthenticationFailureJournalV1 d)
        { await using var c = await reader.OpenConnectionAsync(); await using var t = await c.BeginTransactionAsync(); return await Resolve(c, t, d, true); }
        async Task<long> Head() { await using var c = owner.CreateCommand("SELECT head_sequence FROM security_audit.streams WHERE stream_id=$1"); c.Parameters.AddWithValue(binding.StreamId); return (long)(await c.ExecuteScalarAsync())!; }
        var denied = Descriptor(); var first = await Persist(writer, denied);
        Check(first == await Read(denied) && first == await Persist(writer, denied), "AP-PG01/02 exact outcome receipt resolves");
        var failed = Descriptor(SecurityAuditAction.AuthenticationFailed); await Persist(writer, failed);
        Check(await Scalar("SELECT count(*) FROM ap_pg.outcomes") == 2, "both terminal action/outcome pairs committed");
        Check(await Scalar("SELECT count(*) FROM security_audit.events e JOIN ap_pg.bindings b USING(stream_id) WHERE canonical_event::jsonb->'actor'='null'::jsonb AND canonical_event::jsonb->'target'='null'::jsonb AND canonical_event::jsonb->'sessionReference'='null'::jsonb AND canonical_event::jsonb->'securityVersion'='null'::jsonb AND canonical_event::jsonb->'customerId'='null'::jsonb AND canonical_event::jsonb->'projectId'='null'::jsonb") == 2, "anonymous identity/scope/session/version never fabricated");
        foreach (var changed in new[] { denied with { EventId = Guid.NewGuid() }, denied with { CorrelationId = Guid.NewGuid() },
            denied with { Reason = SecurityAuditReason.ProviderUnavailable }, denied with { OccurredAtUtc = denied.OccurredAtUtc.AddTicks(-1) } })
            await Refused(async () => await Persist(writer, changed), "same operation conflicting descriptor");
        await Refused(async () => await Persist(writer, denied with { OperationId = Guid.NewGuid() }), "event ID substituted operation");
        await Refused(async () => await Persist(writer, denied with { Binding = binding with { EnvironmentId = "foreign" } }), "full binding mismatch");
        await Refused(async () => await Persist(writer, denied with { ActorKind = SecurityAuditActorKind.Human, Subject = binding.Writer }), "anonymous-only fixture");
        await Refused(() => Sql(writer, "SELECT ap_pg.record_outcome($1,$2,$3)", binding.StreamId, Bytes(denied), new string('0', 64)), "SQL mismatched digest");
        var race = Descriptor(); var raced = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Persist(writer, race)));
        Check(raced.All(r => r == raced[0]), "AP-PG03 concurrent exact retry one receipt");
        var distinct = Enumerable.Range(0, 12).Select(_ => Descriptor()).ToArray();
        var results = await Task.WhenAll(distinct.Select(d => Persist(writer, d)));
        Check(results.Select(r => r.EventId).Distinct().Count() == 12, "distinct concurrent attempts remain distinct");
        var sourceDescriptor = Descriptor(); var source = new Source(journal, sourceDescriptor.EventId); // fixture reference derives from event ID only
        var imports = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Persist(reconciler, sourceDescriptor, source)));
        Check(imports.All(r => r == imports[0]) && await Scalar("SELECT count(*) FROM ap_pg.sources") == 1, "AP-PG04 repeated reconciliation one event/source");
        var before = await Head();
        await Persist(reconciler, denied, new(journal, denied.EventId));
        await Persist(reconciler, denied, new(otherJournal, denied.EventId));
        Check(await Head() == before && await Scalar("SELECT count(*) FROM ap_pg.sources") == 3, "direct-to-source and two source bindings never append another event");
        var crossed = Descriptor(); var crossedSource = new Source(journal, crossed.EventId);
        var crossResults = await Task.WhenAll(Persist(writer, crossed), Persist(reconciler, crossed, crossedSource));
        Check(crossResults[0] == crossResults[1], "direct/reconcile race shares operation lock and one event");
        await Refused(async () => await Persist(reconciler, sourceDescriptor, source with { StorageReference = Guid.NewGuid() }), "source reference conflict");
        await Refused(async () => await Persist(reconciler, sourceDescriptor, source with { JournalBinding = Guid.NewGuid() }), "unknown source binding");
        await Refused(async () => await Persist(writer, sourceDescriptor, source), "writer cannot link source");
        await Refused(() => Sql(reconciler, "SELECT ap_pg.record_source($1,$2,$3,$4,$5)", binding.StreamId, Bytes(sourceDescriptor), new string('0', 64), journal, sourceDescriptor.EventId), "source digest conflict");
        var rollback = Descriptor(); before = await Head();
        try { await Persist(reconciler, rollback, new(journal, rollback.EventId), true); throw new Exception("Expected rollback injection"); }
        catch (IOException) { count++; }
        Check(await Head() == before && await Read(rollback) is null, "AP-PG05 intermediate failure rolls back head/event/outcome/source");
        await Refused(async () =>
        {
            var d = Descriptor(); await using var c = await writer.OpenConnectionAsync(); await using var t = await c.BeginTransactionAsync();
            await audit.AppendAsync(c, t, new(d.EventId, d.OperationId, d.OccurredAtUtc, d.ActorKind, null, d.CorrelationId, d.Action, d.Outcome, d.Reason, null, null, null, null));
            await t.CommitAsync();
        }, "standalone authentication event lacks deferred outcome obligation");
        Check(await Head() == before, "deferred event failure rolls back stream head");
        await Refused(async () =>
        {
            var d = Descriptor(); await using var c = await writer.OpenConnectionAsync(); await using var t = await c.BeginTransactionAsync();
            await audit.AppendAsync(c, t, new(d.EventId, d.OperationId, d.OccurredAtUtc, d.ActorKind, null, Guid.NewGuid(), d.Action, d.Outcome, d.Reason, null, null, null, null));
            await using var q = new NpgsqlCommand("SELECT ap_pg.record_outcome($1,$2,$3)", c, t);
            q.Parameters.AddWithValue(binding.StreamId); q.Parameters.AddWithValue(Bytes(d));
            q.Parameters.AddWithValue(AuthenticationFailureJournalCodec.Digest(d, binding, clock.Now));
            await q.ExecuteNonQueryAsync(); await t.CommitAsync();
        }, "database exact descriptor/event projection rejects correlation substitution");
        Check(await Head() == before, "projection failure rolls back event/outcome/head");
        var unknown = Descriptor(); Outcome? committedUnknown = null;
        try { committedUnknown = await Persist(reconciler, unknown, new(journal, unknown.EventId)); throw new IOException("Synthetic acknowledgment discarded after actual COMMIT."); }
        catch (IOException) { count++; }
        before = await Head();
        Check(await Read(unknown) == committedUnknown && await Persist(reconciler, unknown, new(journal, unknown.EventId)) == committedUnknown
            && await Head() == before, "actual COMMIT plus simulated acknowledgment loss resolves exact metadata without replay");
        Console.WriteLine("PASS AP-PG05: actual COMMIT followed by deterministic discarded acknowledgment; not an actual TCP interruption.");
        var waited = Descriptor(); var occurrence = waited.OccurredAtUtc.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);
        await using (var c = await owner.OpenConnectionAsync())
        await using (var t = await c.BeginTransactionAsync())
        {
            await using var hold = new NpgsqlCommand("SELECT stream_id FROM security_audit.streams WHERE stream_id=$1 FOR UPDATE", c, t);
            hold.Parameters.AddWithValue(binding.StreamId); await hold.ExecuteScalarAsync();
            var waiting = Persist(reconciler, waited, new(journal, waited.EventId)); var observed = false;
            for (var attempt = 0; attempt < 200 && !observed; attempt++)
            {
                await using var query = owner.CreateCommand("SELECT EXISTS(SELECT 1 FROM pg_stat_activity WHERE datname=current_database() AND usename='iga_ap_reconciler' AND wait_event_type='Lock' AND query LIKE '%lock_head%')");
                observed = (bool)(await query.ExecuteScalarAsync())!; if (!observed) await Task.Delay(10);
            }
            Check(observed && !waiting.IsCompleted, "AP-PG06 actual head lock wait observed");
            clock.Now = clock.Now.AddMinutes(1); await t.CommitAsync(); await waiting;
        }
        await using (var times = owner.CreateCommand("SELECT original_occurrence,reconciled_at FROM ap_pg.sources WHERE source_event_id=$1"))
        {
            times.Parameters.AddWithValue(waited.EventId); await using var r = await times.ExecuteReaderAsync(); await r.ReadAsync();
            Check(r.GetString(0) == occurrence && occurrence.EndsWith("7Z", StringComparison.Ordinal) && r.GetString(1) == clock.Now.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture), "exact original seventh tick survives; fresh append time sampled after wait");
        }
        // Database-level hostile descriptor corpus: bypass the C# parser through actual restricted LOGIN.
        var canonical = Bytes(denied);
        foreach (var raw in new[] { " " + canonical, canonical[..^1] + ",\"token\":\"synthetic-prohibited\"}",
            canonical[..^1] + ",\"eventId\":\"" + denied.EventId.ToString("D") + "\"}",
            canonical.Replace("Anonymous", "anonymous", StringComparison.Ordinal), canonical.Replace("InvalidProtocol", "0", StringComparison.Ordinal),
            canonical.Replace("\"reason\":\"InvalidProtocol\"", "\"reason\":0", StringComparison.Ordinal),
            canonical.Replace("\"securityVersion\":null", "\"securityVersion\":\"1\"", StringComparison.Ordinal),
            canonical.Replace("synthetic-anonymous", "foreign", StringComparison.Ordinal), canonical.Replace(denied.EventId.ToString("D"), Guid.Empty.ToString("D"), StringComparison.Ordinal),
            canonical.Replace(occurrence, "2999-01-01T00:00:00.0000000Z", StringComparison.Ordinal) })
            await Refused(() => Sql(writer, "SELECT ap_pg.lock_outcome($1,$2,$3)", binding.StreamId, raw, SecurityAuditCanonical.Hash(raw)), "SQL closed descriptor rejects hostile bytes");
        foreach (var sql in new[] { "UPDATE ap_pg.outcomes SET descriptor='{}'", "DELETE FROM ap_pg.sources", "INSERT INTO ap_pg.outcomes SELECT * FROM ap_pg.outcomes",
            "UPDATE ap_pg.bindings SET maximum_bytes=999999", "DELETE FROM security_audit.events", "SELECT ap_pg.record_source(NULL,NULL,NULL,NULL,NULL)",
            "SELECT security_audit.issue_ticket(NULL,NULL,NULL,NULL,1,now(),now(),NULL,NULL)", "SELECT security_audit.purge_event(NULL,now(),NULL)" })
            await Refused(() => Sql(writer, sql), "AP-PG07 restricted writer bypass");
        await Refused(() => Sql(reader, "SELECT security_audit.lock_head($1,$2,$3)", binding.StreamId, binding.EnvironmentId, binding.WriterBindingReference), "reader cannot append");
        await Refused(() => Sql(reconciler, "UPDATE ap_pg.sources SET source_digest=repeat('0',64)"), "reconciler cannot alter source history");
        foreach (var role in new[] { "iga_audit_runtime", "iga_audit_admin", "iga_audit_lifecycle" })
        { await using var deniedRole = As(role); await Refused(() => Sql(deniedRole, "SELECT ap_pg.record_source(NULL,NULL,NULL,NULL,NULL)"), "existing role cannot write source"); }
        await Refused(() => Sql(writer, "SELECT ap_pg.lock_outcome($1,$2,$3)", Guid.NewGuid(), canonical, SecurityAuditCanonical.Hash(canonical)), "foreign stream denied");
        Check(await Scalar("SELECT count(*) FROM pg_roles WHERE rolname='iga_ap_function_owner' AND NOT rolcanlogin AND NOT rolsuper AND NOT rolbypassrls") == 1, "separate NONLOGIN constrained function owner");
        // Actual deferred source substitution must reject COMMIT, not merely an absent eventual read.
        await Refused(() => Sql(owner, "INSERT INTO ap_pg.sources SELECT $1,source_event_id,storage_receipt_reference,source_digest,stream_id,operation_id,'2000-01-01T00:00:00.0000000Z',reconciled_at FROM ap_pg.sources LIMIT 1", Guid.NewGuid()), "deferred invalid source metadata");
        async Task Verify()
        {
            var entries = new List<AuditIntegrityEntryV1>();
            await using (var q = reader.CreateCommand("SELECT event_id,stream_id,sequence,previous_sha256,event_sha256,canonical_event FROM ap_pg.read_events()"))
            await using (var r = await q.ExecuteReaderAsync())
                while (await r.ReadAsync()) entries.Add(new(r.GetGuid(0), r.GetGuid(1), r.GetInt64(2), r.GetString(3), r.GetString(4), r.GetString(5), null));
            var last = entries.OrderBy(e => e.Sequence).Last();
            // Synthetic snapshot only; no independently retained production witness claim.
            AuditIntegrityVerifier.Verify(binding, new(binding.StreamId, binding.EnvironmentId, binding.WriterBindingReference,
                last.Sequence, last.EventSha256, clock.Now, Guid.NewGuid()), last.Sequence, last.EventSha256, entries, []);
            await Sql(owner, "SELECT ap_pg.verify_links()");
        }
        await Verify(); count++;
        await Sql(owner, "UPDATE ap_pg.sources SET original_occurrence='2000-01-01T00:00:00.0000000Z' WHERE source_event_id=$1", waited.EventId);
        await Refused(Verify, "fixture source-link verifier detects altered occurrence");
        await Sql(owner, "UPDATE ap_pg.sources SET original_occurrence=$1 WHERE source_event_id=$2", occurrence, waited.EventId);
        var snapshot = await Scalar("SELECT count(*) FROM ap_pg.outcomes");
        await Sql(owner, "DELETE FROM ap_pg.outcomes WHERE event_id=$1", failed.EventId);
        await Refused(Verify, "fixture link verifier detects missing anonymous outcome");
        await Sql(owner, "INSERT INTO ap_pg.outcomes SELECT e.stream_id,e.operation_id,$1,$2,e.event_id,e.sequence,e.event_sha256 FROM security_audit.events e WHERE e.event_id=$3", Bytes(failed), AuthenticationFailureJournalCodec.Digest(failed, binding, clock.Now), failed.EventId);
        await Verify(); Check(await Scalar("SELECT count(*) FROM ap_pg.outcomes") == snapshot && await Head() == snapshot, "canonical chain and additive links restored/contiguous");
        Console.WriteLine($"PASS: {count} AP-PG actual PostgreSQL anonymous outcome/source receipt/atomicity/concurrency/restricted-role checks.");
        Console.WriteLine("NOT VERIFIED AP-PG: production receipts/hooks/grants, Blob delivery/create-only enforcement, independent witness, lifecycle/restore, deadlines/capacity or total-outage compliance.");
        return count;
    }

    private const string FixtureSql = """
DROP SCHEMA IF EXISTS ap_pg CASCADE;
CREATE SCHEMA ap_pg;
CREATE TABLE ap_pg.bindings(stream_id uuid PRIMARY KEY REFERENCES security_audit.streams,maximum_bytes integer NOT NULL CHECK(maximum_bytes>0));
CREATE TABLE ap_pg.roles(role_name name NOT NULL,stream_id uuid NOT NULL REFERENCES ap_pg.bindings,can_source boolean NOT NULL,is_reader boolean NOT NULL,PRIMARY KEY(role_name,stream_id));
CREATE TABLE ap_pg.journals(journal_binding uuid PRIMARY KEY CHECK(journal_binding<>'00000000-0000-0000-0000-000000000000'));
CREATE TABLE ap_pg.outcomes(stream_id uuid NOT NULL REFERENCES ap_pg.bindings,operation_id uuid NOT NULL,descriptor text NOT NULL,descriptor_digest text NOT NULL CHECK(descriptor_digest~'^[0-9a-f]{64}$'),event_id uuid NOT NULL UNIQUE REFERENCES security_audit.events DEFERRABLE INITIALLY DEFERRED,sequence bigint NOT NULL CHECK(sequence>0),event_digest text NOT NULL CHECK(event_digest~'^[0-9a-f]{64}$'),PRIMARY KEY(stream_id,operation_id));
CREATE TABLE ap_pg.sources(journal_binding uuid NOT NULL,source_event_id uuid NOT NULL,storage_receipt_reference uuid NOT NULL,source_digest text NOT NULL CHECK(source_digest~'^[0-9a-f]{64}$'),stream_id uuid NOT NULL,operation_id uuid NOT NULL,original_occurrence text NOT NULL,reconciled_at text NOT NULL,PRIMARY KEY(journal_binding,source_event_id),UNIQUE(journal_binding,storage_receipt_reference),FOREIGN KEY(stream_id,operation_id) REFERENCES ap_pg.outcomes DEFERRABLE INITIALLY DEFERRED);
CREATE FUNCTION ap_pg.descriptor(p_stream uuid,p_descriptor text,p_digest text) RETURNS jsonb LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
DECLARE d jsonb;s security_audit.streams%ROWTYPE; maximum integer;k text;v text;
BEGIN
 SELECT b.maximum_bytes INTO STRICT maximum FROM ap_pg.bindings b WHERE b.stream_id=p_stream;
 SELECT * INTO STRICT s FROM security_audit.streams WHERE stream_id=p_stream;
 IF p_descriptor IS NULL OR p_digest IS NULL OR octet_length(p_descriptor)>maximum OR p_digest IS DISTINCT FROM encode(sha256(convert_to(p_descriptor,'UTF8')),'hex') THEN RAISE EXCEPTION 'Fixture descriptor digest/size denied'; END IF;
 d:=p_descriptor::jsonb;
 IF security_audit.has_duplicate_fields(p_descriptor::json) OR p_descriptor IS DISTINCT FROM security_audit.canonical_json(d) OR jsonb_typeof(d) IS DISTINCT FROM 'object' OR (SELECT count(*) FROM jsonb_object_keys(d))<>17 OR NOT d ?& ARRAY['action','actorKind','applicationClientId','correlationId','environmentId','eventId','occurredAtUtc','operationId','outcome','reason','schemaVersion','securityVersion','sessionReference','streamId','subject','writer','writerBindingReference'] THEN RAISE EXCEPTION 'Fixture descriptor closed bytes denied'; END IF;
 FOREACH k IN ARRAY ARRAY['action','actorKind','applicationClientId','correlationId','environmentId','eventId','occurredAtUtc','operationId','outcome','reason','schemaVersion','streamId','writerBindingReference'] LOOP
  IF jsonb_typeof(d->k) IS DISTINCT FROM 'string' THEN RAISE EXCEPTION 'Fixture descriptor scalar denied'; END IF;
 END LOOP;
 IF d->>'schemaVersion' IS DISTINCT FROM 'authentication-failure-journal-v1' OR d->>'actorKind' IS DISTINCT FROM 'Anonymous' OR d->'subject' IS DISTINCT FROM 'null'::jsonb OR d->'sessionReference' IS DISTINCT FROM 'null'::jsonb OR d->'securityVersion' IS DISTINCT FROM 'null'::jsonb OR NOT ((d->>'action'='AuthenticationDenied' AND d->>'outcome'='Denied') OR (d->>'action'='AuthenticationFailed' AND d->>'outcome'='Failed')) OR d->>'reason' NOT IN ('None','InvalidProtocol','AuthorityDenied','ProviderUnavailable','AuditUnavailable','Conflict') OR d->>'occurredAtUtc' !~ '^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}\.[0-9]{7}Z$' OR (d->>'occurredAtUtc')::timestamptz>clock_timestamp() THEN RAISE EXCEPTION 'Fixture anonymous shape/time denied'; END IF;
 IF d->>'streamId' IS DISTINCT FROM s.stream_id::text OR d->>'environmentId' IS DISTINCT FROM s.environment_id OR d->>'writerBindingReference' IS DISTINCT FROM s.writer_binding_reference::text OR d->>'applicationClientId' IS DISTINCT FROM s.application_client_id::text OR d->'writer' IS DISTINCT FROM jsonb_build_object('tenantId',s.writer_tenant_id::text,'objectId',s.writer_object_id::text) THEN RAISE EXCEPTION 'Fixture full binding denied'; END IF;
 FOREACH k IN ARRAY ARRAY['eventId','operationId','correlationId'] LOOP
  v:=d->>k;
  IF v !~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' OR v='00000000-0000-0000-0000-000000000000' THEN RAISE EXCEPTION 'Fixture nonempty canonical ID denied'; END IF;
 END LOOP;
 RETURN d;
END $$;
CREATE FUNCTION ap_pg.access(p_stream uuid,p_write boolean,p_source boolean DEFAULT false) RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
BEGIN
 IF NOT EXISTS(SELECT 1 FROM ap_pg.roles WHERE role_name=session_user AND stream_id=p_stream AND (NOT p_write OR NOT is_reader) AND (NOT p_source OR can_source)) THEN RAISE EXCEPTION 'Fixture role/binding denied'; END IF;
END $$;
CREATE FUNCTION ap_pg.read_outcome(p_stream uuid,p_descriptor text,p_digest text) RETURNS SETOF ap_pg.outcomes LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
DECLARE d jsonb;r ap_pg.outcomes%ROWTYPE;
BEGIN
 PERFORM ap_pg.access(p_stream,false);d:=ap_pg.descriptor(p_stream,p_descriptor,p_digest);
 SELECT * INTO r FROM ap_pg.outcomes WHERE stream_id=p_stream AND operation_id=(d->>'operationId')::uuid;
 IF FOUND THEN
  IF r.descriptor IS DISTINCT FROM p_descriptor OR r.descriptor_digest IS DISTINCT FROM p_digest THEN RAISE EXCEPTION 'Fixture operation conflict'; END IF;
  RETURN NEXT r;
 END IF;
END $$;
CREATE FUNCTION ap_pg.lock_outcome(p_stream uuid,p_descriptor text,p_digest text) RETURNS SETOF ap_pg.outcomes LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
DECLARE d jsonb;
BEGIN
 PERFORM ap_pg.access(p_stream,true);d:=ap_pg.descriptor(p_stream,p_descriptor,p_digest);
 PERFORM pg_advisory_xact_lock(hashtextextended('ap-pg:'||p_stream::text||':'||(d->>'operationId'),0));
 RETURN QUERY SELECT * FROM ap_pg.read_outcome(p_stream,p_descriptor,p_digest);
END $$;
CREATE FUNCTION ap_pg.check_outcome(p_stream uuid,p_operation uuid) RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
DECLARE r ap_pg.outcomes%ROWTYPE;d jsonb;e security_audit.events%ROWTYPE;j jsonb;expected jsonb;
BEGIN
 SELECT * INTO STRICT r FROM ap_pg.outcomes WHERE stream_id=p_stream AND operation_id=p_operation;
 d:=ap_pg.descriptor(p_stream,r.descriptor,r.descriptor_digest);
 SELECT * INTO STRICT e FROM security_audit.events WHERE event_id=r.event_id;
 j:=e.canonical_event::jsonb;
 expected:=jsonb_build_object('action',d->'action','actor',NULL,'actorKind','Anonymous','applicationClientId',d->'applicationClientId','authenticationEvidence','None','correlationId',d->'correlationId','customerId',NULL,'environmentId',d->'environmentId','eventAtUtc',j->'eventAtUtc','eventId',d->'eventId','operationId',d->'operationId','outcome',d->'outcome','previousEventSha256',e.previous_sha256,'previousSessionReference',NULL,'projectId',NULL,'reason',d->'reason','schemaVersion','security-audit-session-v1','scope','Platform','securityVersion',NULL,'sequence',e.sequence::text,'sessionReference',NULL,'streamId',d->'streamId','target',NULL,'writer',d->'writer','writerBindingReference',d->'writerBindingReference');
 IF j IS DISTINCT FROM expected OR e.stream_id IS DISTINCT FROM p_stream OR e.operation_id IS DISTINCT FROM p_operation OR r.event_id::text IS DISTINCT FROM d->>'eventId' OR p_operation::text IS DISTINCT FROM d->>'operationId' OR r.sequence IS DISTINCT FROM e.sequence OR r.event_digest IS DISTINCT FROM e.event_sha256 OR e.event_sha256 IS DISTINCT FROM encode(sha256(convert_to(e.canonical_event,'UTF8')),'hex') OR (j->>'eventAtUtc') COLLATE "C" < (d->>'occurredAtUtc') COLLATE "C" THEN RAISE EXCEPTION 'Fixture outcome projection denied'; END IF;
END $$;
CREATE FUNCTION ap_pg.record_outcome(p_stream uuid,p_descriptor text,p_digest text) RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
DECLARE d jsonb;e security_audit.events%ROWTYPE;
BEGIN
 PERFORM ap_pg.access(p_stream,true);d:=ap_pg.descriptor(p_stream,p_descriptor,p_digest);
 PERFORM pg_advisory_xact_lock(hashtextextended('ap-pg:'||p_stream::text||':'||(d->>'operationId'),0));
 IF EXISTS(SELECT 1 FROM ap_pg.outcomes WHERE stream_id=p_stream AND operation_id=(d->>'operationId')::uuid) THEN
  PERFORM ap_pg.read_outcome(p_stream,p_descriptor,p_digest);RETURN;
 END IF;
 SELECT * INTO STRICT e FROM security_audit.events WHERE event_id=(d->>'eventId')::uuid;
 INSERT INTO ap_pg.outcomes VALUES(p_stream,(d->>'operationId')::uuid,p_descriptor,p_digest,e.event_id,e.sequence,e.event_sha256);
 PERFORM ap_pg.check_outcome(p_stream,(d->>'operationId')::uuid);
END $$;
CREATE FUNCTION ap_pg.check_source(p_journal uuid,p_event uuid) RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
DECLARE s ap_pg.sources%ROWTYPE;r ap_pg.outcomes%ROWTYPE;e security_audit.events%ROWTYPE;
BEGIN
 SELECT * INTO STRICT s FROM ap_pg.sources WHERE journal_binding=p_journal AND source_event_id=p_event;
 SELECT * INTO STRICT r FROM ap_pg.outcomes WHERE stream_id=s.stream_id AND operation_id=s.operation_id;
 SELECT * INTO STRICT e FROM security_audit.events WHERE event_id=r.event_id;
 IF NOT EXISTS(SELECT 1 FROM ap_pg.journals WHERE journal_binding=s.journal_binding) OR s.source_event_id IS DISTINCT FROM r.event_id OR s.storage_receipt_reference IS DISTINCT FROM s.source_event_id OR s.source_digest IS DISTINCT FROM r.descriptor_digest OR s.original_occurrence IS DISTINCT FROM r.descriptor::jsonb->>'occurredAtUtc' OR s.reconciled_at IS DISTINCT FROM e.canonical_event::jsonb->>'eventAtUtc' THEN RAISE EXCEPTION 'Fixture source link denied'; END IF;
 PERFORM ap_pg.check_outcome(s.stream_id,s.operation_id);
END $$;
CREATE FUNCTION ap_pg.record_source(p_stream uuid,p_descriptor text,p_digest text,p_journal uuid,p_storage uuid) RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
DECLARE d jsonb;r ap_pg.outcomes%ROWTYPE;existing ap_pg.sources%ROWTYPE;reconciled text;
BEGIN
 PERFORM ap_pg.access(p_stream,true,true);d:=ap_pg.descriptor(p_stream,p_descriptor,p_digest);
 PERFORM pg_advisory_xact_lock(hashtextextended('ap-pg:'||p_stream::text||':'||(d->>'operationId'),0));
 IF p_journal IS NULL OR p_storage IS NULL OR NOT EXISTS(SELECT 1 FROM ap_pg.journals WHERE journal_binding=p_journal) OR p_storage::text IS DISTINCT FROM d->>'eventId' THEN RAISE EXCEPTION 'Fixture synthetic source reference denied'; END IF;
 SELECT * INTO STRICT r FROM ap_pg.read_outcome(p_stream,p_descriptor,p_digest);
 SELECT * INTO existing FROM ap_pg.sources WHERE journal_binding=p_journal AND source_event_id=r.event_id;
 IF FOUND THEN PERFORM ap_pg.check_source(p_journal,r.event_id);RETURN; END IF;
 SELECT canonical_event::jsonb->>'eventAtUtc' INTO STRICT reconciled FROM security_audit.events WHERE event_id=r.event_id;
 INSERT INTO ap_pg.sources VALUES(p_journal,r.event_id,p_storage,p_digest,p_stream,r.operation_id,d->>'occurredAtUtc',reconciled);
 PERFORM ap_pg.check_source(p_journal,r.event_id);
END $$;
CREATE FUNCTION ap_pg.event_obligation() RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
BEGIN
 IF EXISTS(SELECT 1 FROM ap_pg.bindings WHERE stream_id=NEW.stream_id) THEN
  IF NOT EXISTS(SELECT 1 FROM ap_pg.outcomes WHERE stream_id=NEW.stream_id AND operation_id=NEW.operation_id AND event_id=NEW.event_id) THEN RAISE EXCEPTION 'Fixture event requires anonymous outcome'; END IF;
  PERFORM ap_pg.check_outcome(NEW.stream_id,NEW.operation_id);
 END IF;
 RETURN NULL;
END $$;
CREATE CONSTRAINT TRIGGER ap_pg_event_obligation AFTER INSERT ON security_audit.events DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION ap_pg.event_obligation();
CREATE FUNCTION ap_pg.receipt_obligation() RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
BEGIN
 IF TG_TABLE_NAME='outcomes' THEN PERFORM ap_pg.check_outcome(NEW.stream_id,NEW.operation_id);
 ELSE PERFORM ap_pg.check_source(NEW.journal_binding,NEW.source_event_id);END IF;
 RETURN NULL;
END $$;
CREATE CONSTRAINT TRIGGER ap_pg_outcome_obligation AFTER INSERT ON ap_pg.outcomes DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION ap_pg.receipt_obligation();
CREATE CONSTRAINT TRIGGER ap_pg_source_obligation AFTER INSERT ON ap_pg.sources DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION ap_pg.receipt_obligation();
CREATE FUNCTION ap_pg.read_events() RETURNS SETOF security_audit.events LANGUAGE sql SECURITY DEFINER SET search_path=pg_catalog AS $$
 SELECT e.* FROM security_audit.events e JOIN ap_pg.roles r USING(stream_id) WHERE r.role_name=session_user;
$$;
CREATE FUNCTION ap_pg.verify_links() RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
DECLARE r record;
BEGIN
 FOR r IN SELECT e.* FROM security_audit.events e JOIN ap_pg.bindings b USING(stream_id) LOOP
  IF NOT EXISTS(SELECT 1 FROM ap_pg.outcomes o WHERE o.event_id=r.event_id AND o.operation_id=r.operation_id AND o.stream_id=r.stream_id) THEN RAISE EXCEPTION 'Fixture missing outcome';END IF;
 END LOOP;
 FOR r IN SELECT * FROM ap_pg.outcomes LOOP PERFORM ap_pg.check_outcome(r.stream_id,r.operation_id);END LOOP;
 FOR r IN SELECT * FROM ap_pg.sources LOOP PERFORM ap_pg.check_source(r.journal_binding,r.source_event_id);END LOOP;
END $$;
DO $$ BEGIN
 IF NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='iga_ap_function_owner') THEN CREATE ROLE iga_ap_function_owner NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;END IF;
 IF NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='iga_ap_outcome_writer') THEN CREATE ROLE iga_ap_outcome_writer LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;END IF;
 IF NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='iga_ap_reconciler') THEN CREATE ROLE iga_ap_reconciler LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;END IF;
 IF NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='iga_ap_reader') THEN CREATE ROLE iga_ap_reader LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;END IF;
END $$;
REVOKE ALL ON ALL TABLES IN SCHEMA ap_pg FROM PUBLIC;
REVOKE ALL ON ALL FUNCTIONS IN SCHEMA ap_pg FROM PUBLIC;
GRANT USAGE ON SCHEMA ap_pg,security_audit TO iga_ap_function_owner,iga_ap_outcome_writer,iga_ap_reconciler,iga_ap_reader;
GRANT SELECT,INSERT ON ALL TABLES IN SCHEMA ap_pg TO iga_ap_function_owner;
GRANT SELECT ON security_audit.streams,security_audit.events TO iga_ap_function_owner;
GRANT EXECUTE ON FUNCTION security_audit.has_duplicate_fields(json),security_audit.canonical_json(jsonb) TO iga_ap_function_owner;
DO $$ DECLARE f record;BEGIN
 FOR f IN SELECT p.oid::regprocedure signature FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname='ap_pg' LOOP EXECUTE 'ALTER FUNCTION '||f.signature||' OWNER TO iga_ap_function_owner';END LOOP;
END $$;
GRANT EXECUTE ON FUNCTION security_audit.lock_head(uuid,text,uuid),security_audit.append_event(uuid,uuid,text,text) TO iga_ap_outcome_writer,iga_ap_reconciler;
GRANT EXECUTE ON FUNCTION ap_pg.lock_outcome(uuid,text,text),ap_pg.read_outcome(uuid,text,text),ap_pg.record_outcome(uuid,text,text) TO iga_ap_outcome_writer,iga_ap_reconciler;
GRANT EXECUTE ON FUNCTION ap_pg.record_source(uuid,text,text,uuid,uuid) TO iga_ap_reconciler;
GRANT EXECUTE ON FUNCTION ap_pg.read_outcome(uuid,text,text),ap_pg.read_events() TO iga_ap_reader;
-- Only fixture owner/operator runs the additive integrity comparison; no deployed monitor or witness claim.
""";
}
