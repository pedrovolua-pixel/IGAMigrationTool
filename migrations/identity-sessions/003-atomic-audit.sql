-- Additive, explicitly executed control-plane migration. No login/grant/cloud
-- provisioning, lifecycle scheduling or startup execution. Before runtime grants,
-- a controlled operator assigns these functions/tables to a distinct NONLOGIN
-- owner; local synthetic role tests prove that boundary independently.
CREATE SCHEMA security_audit;
CREATE TABLE security_audit.streams (
    stream_id uuid PRIMARY KEY,
    environment_id text NOT NULL CHECK (environment_id ~ '^[a-z][a-z0-9-]{0,47}$'),
    writer_binding_reference uuid NOT NULL,
    writer_tenant_id uuid NOT NULL,
    writer_object_id uuid NOT NULL,
    application_client_id uuid NOT NULL,
    head_sequence bigint NOT NULL DEFAULT 0 CHECK (head_sequence >= 0),
    head_sha256 text NOT NULL DEFAULT repeat('0',64) CHECK (head_sha256 ~ '^[0-9a-f]{64}$'),
    revision bigint NOT NULL DEFAULT 1 CHECK (revision > 0)
);
CREATE TABLE security_audit.events (
    event_id uuid PRIMARY KEY, operation_id uuid NOT NULL,
    stream_id uuid NOT NULL REFERENCES security_audit.streams(stream_id),
    sequence bigint NOT NULL CHECK (sequence > 0),
    event_at_utc timestamptz NOT NULL,
    previous_sha256 text NOT NULL CHECK (previous_sha256 ~ '^[0-9a-f]{64}$'),
    event_sha256 text NOT NULL CHECK (event_sha256 ~ '^[0-9a-f]{64}$'),
    canonical_event text NOT NULL,
    UNIQUE(stream_id,sequence)
);
CREATE TABLE security_audit.operation_receipts (
    operation_id uuid PRIMARY KEY,
    request_canonical text NOT NULL,
    receipt_canonical text NOT NULL,
    event_ids uuid[] NOT NULL CHECK (cardinality(event_ids)>0)
);
CREATE TABLE security_audit.lifecycle (
    event_id uuid PRIMARY KEY REFERENCES security_audit.events(event_id),
    soft_deleted_at timestamptz, hold_reference uuid, hold_expires_at timestamptz,
    lifecycle_authority uuid NOT NULL
);
CREATE TABLE security_audit.tombstones (
    event_id uuid PRIMARY KEY, stream_id uuid NOT NULL REFERENCES security_audit.streams(stream_id),
    sequence bigint NOT NULL CHECK(sequence>0), previous_sha256 text NOT NULL,
    event_sha256 text NOT NULL, purged_at timestamptz NOT NULL,
    lifecycle_authority uuid NOT NULL, UNIQUE(stream_id,sequence)
);
CREATE TABLE security_audit.checkpoints (
    witness_reference uuid PRIMARY KEY,
    stream_id uuid NOT NULL REFERENCES security_audit.streams(stream_id),
    environment_id text NOT NULL, writer_binding_reference uuid NOT NULL,
    head_sequence bigint NOT NULL CHECK(head_sequence>=0), head_sha256 text NOT NULL,
    captured_at timestamptz NOT NULL
);
CREATE VIEW security_audit.visible_events AS
SELECT e.* FROM security_audit.events e LEFT JOIN security_audit.lifecycle l USING(event_id)
WHERE l.soft_deleted_at IS NULL;

CREATE TABLE security_audit.writer_roles(stream_id uuid NOT NULL REFERENCES security_audit.streams(stream_id), role_name name NOT NULL, writer_binding_reference uuid NOT NULL, allowed_actions text[] NOT NULL CHECK(cardinality(allowed_actions)>0), PRIMARY KEY(stream_id,role_name));
CREATE TABLE security_audit.lifecycle_bindings(role_name name PRIMARY KEY,lifecycle_authority uuid NOT NULL);
CREATE TABLE security_audit.reader_scopes(role_name name NOT NULL,stream_id uuid NOT NULL REFERENCES security_audit.streams(stream_id),allow_platform boolean NOT NULL,customer_id uuid,project_id uuid,actor_tenant_id uuid,actor_object_id uuid, CHECK((customer_id IS NULL)=(project_id IS NULL)), CHECK((actor_tenant_id IS NULL)=(actor_object_id IS NULL)));
CREATE TABLE security_audit.witness_bindings(role_name name PRIMARY KEY,stream_id uuid NOT NULL REFERENCES security_audit.streams(stream_id));
CREATE OR REPLACE VIEW security_audit.visible_events AS
SELECT e.* FROM security_audit.events e LEFT JOIN security_audit.lifecycle l USING(event_id)
WHERE l.soft_deleted_at IS NULL AND EXISTS(SELECT 1 FROM security_audit.reader_scopes s WHERE s.role_name=session_user AND s.stream_id=e.stream_id
 AND ((s.allow_platform AND e.canonical_event::jsonb->>'scope'='Platform') OR (s.customer_id::text=e.canonical_event::jsonb->>'customerId' AND s.project_id::text=e.canonical_event::jsonb->>'projectId'))
 AND (s.actor_tenant_id IS NULL OR e.canonical_event::jsonb->'actor'=jsonb_build_object('tenantId',s.actor_tenant_id::text,'objectId',s.actor_object_id::text)));

CREATE FUNCTION security_audit.lock_head(p_stream uuid,p_environment text,p_binding uuid)
RETURNS TABLE(sequence bigint,digest text) LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM security_audit.writer_roles WHERE stream_id=p_stream AND role_name=session_user AND writer_binding_reference=p_binding) THEN RAISE EXCEPTION 'Wrong audit writer role'; END IF;
    RETURN QUERY SELECT s.head_sequence,s.head_sha256 FROM security_audit.streams s
    WHERE s.stream_id=p_stream AND s.environment_id=p_environment AND s.writer_binding_reference=p_binding FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'Audit stream binding unavailable'; END IF;
END $$;

CREATE FUNCTION security_audit.append_event(p_stream uuid,p_binding uuid,p_canonical text,p_digest text)
RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
DECLARE s security_audit.streams%ROWTYPE; j jsonb; event_time timestamptz; n bigint;
BEGIN
    IF NOT EXISTS(SELECT 1 FROM security_audit.writer_roles WHERE stream_id=p_stream AND role_name=session_user AND writer_binding_reference=p_binding) THEN RAISE EXCEPTION 'Wrong audit writer role'; END IF;
    SELECT * INTO STRICT s FROM security_audit.streams WHERE stream_id=p_stream FOR UPDATE;
    j:=p_canonical::jsonb;
    IF NOT EXISTS(SELECT 1 FROM security_audit.writer_roles WHERE stream_id=p_stream AND role_name=session_user AND writer_binding_reference=p_binding AND j->>'action'=ANY(allowed_actions)) THEN RAISE EXCEPTION 'Audit action not allowed to writer role'; END IF;
    IF jsonb_typeof(j)<>'object' OR (SELECT count(*) FROM jsonb_object_keys(j))<>25 OR
       NOT j ?& ARRAY['action','actor','actorKind','applicationClientId','authenticationEvidence','correlationId','customerId','environmentId','eventAtUtc','eventId','operationId','outcome','previousEventSha256','previousSessionReference','projectId','reason','schemaVersion','scope','securityVersion','sequence','sessionReference','streamId','target','writer','writerBindingReference'] OR
       j->>'schemaVersion'<>'security-audit-session-v1' OR j->>'authenticationEvidence'<>'None' OR
       j->>'action' NOT IN ('SessionIssued','AuthenticationDenied','AuthenticationFailed','SessionRevoked','SessionRotated','SubjectRevoked','AuthorityChanged') OR
       j->>'outcome' NOT IN ('Succeeded','Denied','Failed') OR j->>'reason' NOT IN ('None','InvalidProtocol','AuthorityDenied','ProviderUnavailable','AuditUnavailable','Conflict') OR
       j->>'actorKind' NOT IN ('Human','Workload','Anonymous') OR
       (j->>'actorKind'='Anonymous') <> (j->'actor'='null'::jsonb) OR
       p_binding<>s.writer_binding_reference OR j->>'writerBindingReference'<>s.writer_binding_reference::text OR
       j->>'environmentId'<>s.environment_id OR j->>'streamId'<>s.stream_id::text OR
       j->>'applicationClientId'<>s.application_client_id::text OR
       j->'writer'<>jsonb_build_object('tenantId',s.writer_tenant_id::text,'objectId',s.writer_object_id::text) OR
       p_digest<>encode(sha256(convert_to(p_canonical,'UTF8')),'hex') THEN RAISE EXCEPTION 'Closed audit event refused'; END IF;
    n:=(j->>'sequence')::bigint;
    IF n<>s.head_sequence+1 OR j->>'previousEventSha256'<>s.head_sha256 THEN RAISE EXCEPTION 'Audit ordering refused'; END IF;
    event_time:=(j->>'eventAtUtc')::timestamptz;
    IF event_time>clock_timestamp() THEN RAISE EXCEPTION 'Future audit event refused by database clock'; END IF;
    IF EXISTS(SELECT 1 FROM security_audit.events WHERE stream_id=p_stream AND event_at_utc>event_time) THEN RAISE EXCEPTION 'Audit clock regression'; END IF;
    INSERT INTO security_audit.events VALUES ((j->>'eventId')::uuid,(j->>'operationId')::uuid,p_stream,n,event_time,s.head_sha256,p_digest,p_canonical);
    UPDATE security_audit.streams SET head_sequence=n,head_sha256=p_digest,revision=revision+1 WHERE stream_id=p_stream;
END $$;

CREATE FUNCTION security_audit.read_receipt(p_operation uuid)
RETURNS TABLE(request_canonical text,receipt_canonical text) LANGUAGE sql SECURITY DEFINER SET search_path=pg_catalog AS $$
    SELECT r.request_canonical,r.receipt_canonical FROM security_audit.operation_receipts r WHERE r.operation_id=p_operation
    AND EXISTS(SELECT 1 FROM (SELECT event_id,stream_id FROM security_audit.events UNION ALL SELECT event_id,stream_id FROM security_audit.tombstones) e JOIN security_audit.writer_roles w USING(stream_id) WHERE e.event_id=ANY(r.event_ids) AND w.role_name=session_user)

$$;
CREATE FUNCTION security_audit.append_receipt(p_operation uuid,p_request text,p_receipt text,p_events uuid[])
RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
DECLARE j jsonb; r jsonb;
BEGIN
    j:=p_request::jsonb; r:=p_receipt::jsonb;
    IF EXISTS(SELECT 1 FROM security_audit.events e WHERE e.event_id=ANY(p_events) AND NOT EXISTS(
       SELECT 1 FROM security_audit.writer_roles w JOIN security_audit.streams s USING(stream_id)
       WHERE w.stream_id=e.stream_id AND w.role_name=session_user AND w.writer_binding_reference=s.writer_binding_reference
       AND j->>'operationKind'=ANY(w.allowed_actions))) THEN RAISE EXCEPTION 'Receipt writer/action denied'; END IF;
    IF j->>'schemaVersion'<>'operation-receipt-request-v1' OR j->>'operationId'<>p_operation::text OR
       (SELECT count(*) FROM jsonb_object_keys(j))<>13 OR cardinality(p_events)=0 OR
       cardinality(p_events)<>(SELECT count(DISTINCT x) FROM unnest(p_events) x) OR
       (SELECT count(*) FROM security_audit.events WHERE event_id=ANY(p_events) AND operation_id=p_operation)<>cardinality(p_events) OR
       EXISTS(SELECT 1 FROM security_audit.events e JOIN security_audit.streams s USING(stream_id)
           WHERE e.event_id=ANY(p_events) AND j->'issuer'<>jsonb_build_object('tenantId',s.writer_tenant_id::text,'objectId',s.writer_object_id::text))
       THEN RAISE EXCEPTION 'Closed receipt refused'; END IF;
    INSERT INTO security_audit.operation_receipts VALUES(p_operation,p_request,p_receipt,p_events);
END $$;

ALTER TABLE identity_sessions.tickets ADD COLUMN issue_operation_id uuid REFERENCES security_audit.operation_receipts(operation_id) DEFERRABLE INITIALLY DEFERRED;
ALTER TABLE identity_sessions.tickets ADD COLUMN revoke_operation_id uuid REFERENCES security_audit.operation_receipts(operation_id) DEFERRABLE INITIALLY DEFERRED;
CREATE FUNCTION security_audit.issue_ticket(p_hash text,p_reference uuid,p_tenant uuid,p_object uuid,p_version bigint,p_now timestamptz,p_expiry timestamptz,p_payload bytea,p_operation uuid)
RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
BEGIN
 IF p_operation IS NULL OR p_operation='00000000-0000-0000-0000-000000000000' THEN RAISE EXCEPTION 'Atomic operation required'; END IF;
    INSERT INTO identity_sessions.tickets(key_hash,session_reference,tenant_id,object_id,security_version,created_at,last_seen_at,absolute_expires_at,protected_ticket,issue_operation_id)
    VALUES(p_hash,p_reference,p_tenant,p_object,p_version,p_now,p_now,p_expiry,p_payload,p_operation);
END $$;
CREATE FUNCTION security_audit.revoke_ticket(p_hash text,p_now timestamptz,p_operation uuid)
RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
BEGIN
 IF p_operation IS NULL OR p_operation='00000000-0000-0000-0000-000000000000' THEN RAISE EXCEPTION 'Atomic operation required'; END IF;
    UPDATE identity_sessions.tickets SET revoked_at=p_now,revoke_operation_id=p_operation WHERE key_hash=p_hash AND revoked_at IS NULL;
END $$;
REVOKE ALL ON ALL TABLES IN SCHEMA security_audit FROM PUBLIC;
REVOKE ALL ON ALL FUNCTIONS IN SCHEMA security_audit FROM PUBLIC;

CREATE FUNCTION security_audit.lock_subject(p_tenant uuid,p_object uuid)
RETURNS TABLE(active boolean,security_version bigint,provider_checked_at timestamptz,sign_in_valid_from_at timestamptz)
LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
BEGIN
 PERFORM pg_advisory_xact_lock(hashtextextended(p_tenant::text||':'||p_object::text,0));
 RETURN QUERY SELECT s.active,s.security_version,s.provider_checked_at,s.sign_in_valid_from_at
 FROM identity_sessions.subjects s WHERE s.tenant_id=p_tenant AND s.object_id=p_object FOR UPDATE;
END $$;
CREATE FUNCTION security_audit.lock_ticket(p_hash text)
RETURNS TABLE(security_version bigint,created_at timestamptz,last_seen_at timestamptz,absolute_expires_at timestamptz,revoked boolean,protected_ticket bytea,session_reference uuid)
LANGUAGE sql SECURITY DEFINER SET search_path=pg_catalog AS $$
 SELECT t.security_version,t.created_at,t.last_seen_at,t.absolute_expires_at,t.revoked_at IS NOT NULL,t.protected_ticket,t.session_reference
 FROM identity_sessions.tickets t WHERE t.key_hash=p_hash FOR UPDATE
$$;
CREATE FUNCTION security_audit.touch_ticket(p_hash text,p_now timestamptz)
RETURNS void LANGUAGE sql SECURITY DEFINER SET search_path=pg_catalog AS $$
 UPDATE identity_sessions.tickets SET last_seen_at=p_now WHERE key_hash=p_hash AND revoked_at IS NULL AND last_seen_at<=p_now AND p_now<absolute_expires_at AND p_now<last_seen_at+interval '30 minutes'
$$;

CREATE FUNCTION security_audit.check_ticket_audit()
RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
DECLARE op uuid; expected_ref uuid; valid boolean;
BEGIN
 IF TG_OP='UPDATE' AND OLD.issue_operation_id IS NOT NULL AND OLD.revoked_at IS NULL AND NEW.revoked_at IS NOT NULL AND NEW.revoke_operation_id IS NULL THEN RAISE EXCEPTION 'Audited ticket revoke cannot bypass its receipt'; END IF;
 IF NEW.issue_operation_id IS NOT NULL THEN
  SELECT EXISTS(SELECT 1 FROM security_audit.events e JOIN security_audit.operation_receipts r ON r.operation_id=e.operation_id
   WHERE e.operation_id=NEW.issue_operation_id AND e.event_id=ANY(r.event_ids)
   AND e.canonical_event::jsonb->>'action' IN ('SessionIssued','SessionRotated')
   AND e.canonical_event::jsonb->>'sessionReference'=NEW.session_reference::text
   AND e.canonical_event::jsonb->>'securityVersion'=NEW.security_version::text
   AND e.canonical_event::jsonb->'target'=jsonb_build_object('tenantId',NEW.tenant_id::text,'objectId',NEW.object_id::text)) INTO valid;
  IF NOT valid THEN RAISE EXCEPTION 'Issued ticket lacks exact audit receipt'; END IF;
 END IF;
 IF NEW.revoke_operation_id IS NOT NULL THEN
  SELECT EXISTS(SELECT 1 FROM security_audit.events e JOIN security_audit.operation_receipts r ON r.operation_id=e.operation_id
   WHERE e.operation_id=NEW.revoke_operation_id AND e.event_id=ANY(r.event_ids)
   AND (e.canonical_event::jsonb->>'action'='SessionRevoked' AND e.canonical_event::jsonb->>'sessionReference'=NEW.session_reference::text
     OR e.canonical_event::jsonb->>'action'='SessionRotated' AND e.canonical_event::jsonb->>'previousSessionReference'=NEW.session_reference::text
     OR e.canonical_event::jsonb->>'action' IN ('SubjectRevoked','AuthorityChanged'))
   AND e.canonical_event::jsonb->'target'=jsonb_build_object('tenantId',NEW.tenant_id::text,'objectId',NEW.object_id::text)) INTO valid;
  IF NOT valid THEN RAISE EXCEPTION 'Revoked ticket lacks exact audit receipt'; END IF;
 END IF;
 RETURN NULL;
END $$;
CREATE CONSTRAINT TRIGGER ticket_requires_atomic_audit AFTER INSERT OR UPDATE ON identity_sessions.tickets
DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION security_audit.check_ticket_audit();
CREATE UNIQUE INDEX tickets_issue_operation ON identity_sessions.tickets(issue_operation_id) WHERE issue_operation_id IS NOT NULL;

CREATE FUNCTION security_audit.set_hold(p_event uuid,p_hold uuid,p_expiry timestamptz,p_authority uuid)
RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
BEGIN
 IF p_authority IS NULL OR NOT EXISTS(SELECT 1 FROM security_audit.lifecycle_bindings WHERE role_name=session_user AND lifecycle_authority=p_authority) THEN RAISE EXCEPTION 'Wrong lifecycle authority role'; END IF;
 IF p_hold='00000000-0000-0000-0000-000000000000'::uuid OR p_authority='00000000-0000-0000-0000-000000000000'::uuid THEN RAISE EXCEPTION 'Invalid lifecycle authority'; END IF;
 INSERT INTO security_audit.lifecycle(event_id,hold_reference,hold_expires_at,lifecycle_authority) VALUES(p_event,p_hold,p_expiry,p_authority)
 ON CONFLICT(event_id) DO UPDATE SET hold_reference=p_hold,hold_expires_at=p_expiry,lifecycle_authority=p_authority;
END $$;
CREATE FUNCTION security_audit.release_hold(p_event uuid,p_hold uuid,p_authority uuid)
RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
BEGIN
 IF p_authority IS NULL OR NOT EXISTS(SELECT 1 FROM security_audit.lifecycle_bindings WHERE role_name=session_user AND lifecycle_authority=p_authority) THEN RAISE EXCEPTION 'Wrong lifecycle authority role'; END IF;
 UPDATE security_audit.lifecycle SET hold_reference=NULL,hold_expires_at=NULL,lifecycle_authority=p_authority WHERE event_id=p_event AND hold_reference=p_hold;
 IF NOT FOUND THEN RAISE EXCEPTION 'Exact hold required'; END IF;
END $$;
CREATE FUNCTION security_audit.soft_delete_event(p_event uuid,p_now timestamptz,p_authority uuid)
RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
DECLARE e security_audit.events%ROWTYPE;
BEGIN
 IF p_authority IS NULL OR NOT EXISTS(SELECT 1 FROM security_audit.lifecycle_bindings WHERE role_name=session_user AND lifecycle_authority=p_authority) THEN RAISE EXCEPTION 'Wrong lifecycle authority role'; END IF;
 SELECT * INTO STRICT e FROM security_audit.events WHERE event_id=p_event FOR UPDATE;
 IF p_now IS NULL OR p_now>clock_timestamp() THEN RAISE EXCEPTION 'Untrusted lifecycle time'; END IF;
 IF p_now<e.event_at_utc+interval '12 months' OR EXISTS(SELECT 1 FROM security_audit.lifecycle WHERE event_id=p_event AND hold_reference IS NOT NULL) THEN RAISE EXCEPTION 'Audit lifecycle boundary refused'; END IF;
 INSERT INTO security_audit.lifecycle(event_id,soft_deleted_at,lifecycle_authority) VALUES(p_event,p_now,p_authority)
 ON CONFLICT(event_id) DO UPDATE SET soft_deleted_at=COALESCE(security_audit.lifecycle.soft_deleted_at,p_now),lifecycle_authority=p_authority;
END $$;
CREATE FUNCTION security_audit.purge_event(p_event uuid,p_now timestamptz,p_authority uuid)
RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
DECLARE e security_audit.events%ROWTYPE; l security_audit.lifecycle%ROWTYPE;
BEGIN
 IF p_authority IS NULL OR NOT EXISTS(SELECT 1 FROM security_audit.lifecycle_bindings WHERE role_name=session_user AND lifecycle_authority=p_authority) THEN RAISE EXCEPTION 'Wrong lifecycle authority role'; END IF;
 SELECT * INTO STRICT e FROM security_audit.events WHERE event_id=p_event FOR UPDATE;
 SELECT * INTO STRICT l FROM security_audit.lifecycle WHERE event_id=p_event FOR UPDATE;
 IF p_now IS NULL OR p_now>clock_timestamp() THEN RAISE EXCEPTION 'Untrusted lifecycle time'; END IF;
 IF l.soft_deleted_at IS NULL OR l.hold_reference IS NOT NULL OR p_now<l.soft_deleted_at THEN RAISE EXCEPTION 'Audit purge boundary refused'; END IF;
 INSERT INTO security_audit.tombstones VALUES(e.event_id,e.stream_id,e.sequence,e.previous_sha256,e.event_sha256,p_now,p_authority);
 DELETE FROM security_audit.lifecycle WHERE event_id=p_event;
 DELETE FROM security_audit.events WHERE event_id=p_event;
END $$;
CREATE FUNCTION security_audit.record_checkpoint(p_witness uuid,p_stream uuid,p_environment text,p_binding uuid,p_sequence bigint,p_digest text,p_time timestamptz)
RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
BEGIN
 IF NOT EXISTS(SELECT 1 FROM security_audit.witness_bindings WHERE role_name=session_user AND stream_id=p_stream) THEN RAISE EXCEPTION 'Wrong witness role'; END IF;
 IF p_time IS NULL OR p_time>clock_timestamp() THEN RAISE EXCEPTION 'Untrusted checkpoint time'; END IF;
 IF NOT EXISTS(SELECT 1 FROM security_audit.streams WHERE stream_id=p_stream AND environment_id=p_environment AND writer_binding_reference=p_binding AND head_sequence=p_sequence AND head_sha256=p_digest) THEN RAISE EXCEPTION 'Checkpoint head mismatch'; END IF;
 INSERT INTO security_audit.checkpoints VALUES(p_witness,p_stream,p_environment,p_binding,p_sequence,p_digest,p_time);
END $$;
REVOKE ALL ON ALL FUNCTIONS IN SCHEMA security_audit FROM PUBLIC;

CREATE FUNCTION security_audit.valid_subject(p jsonb,p_nullable boolean)
RETURNS boolean LANGUAGE sql IMMUTABLE SET search_path=pg_catalog AS $$
 SELECT CASE WHEN p='null'::jsonb THEN p_nullable ELSE
 jsonb_typeof(p)='object' AND (SELECT count(*) FROM jsonb_object_keys(p))=2 AND p ?& ARRAY['tenantId','objectId']
 AND p->>'tenantId' ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
 AND p->>'objectId' ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
 AND p->>'tenantId'<>'00000000-0000-0000-0000-000000000000' AND p->>'objectId'<>'00000000-0000-0000-0000-000000000000' END
$$;
CREATE FUNCTION security_audit.has_duplicate_fields(p json)
RETURNS boolean LANGUAGE plpgsql IMMUTABLE SET search_path=pg_catalog AS $$
DECLARE child json;
BEGIN
 IF json_typeof(p)='object' THEN
  IF (SELECT count(*) FROM json_each(p))<>(SELECT count(DISTINCT key) FROM json_each(p)) THEN RETURN true; END IF;
  FOR child IN SELECT value FROM json_each(p) LOOP IF security_audit.has_duplicate_fields(child) THEN RETURN true; END IF; END LOOP;
 ELSIF json_typeof(p)='array' THEN
  FOR child IN SELECT value FROM json_array_elements(p) LOOP IF security_audit.has_duplicate_fields(child) THEN RETURN true; END IF; END LOOP;
 END IF;
 RETURN false;
END $$;
CREATE FUNCTION security_audit.canonical_json(p jsonb)
RETURNS text LANGUAGE plpgsql IMMUTABLE SET search_path=pg_catalog AS $$
DECLARE result text;
BEGIN
 CASE jsonb_typeof(p)
 WHEN 'null' THEN RETURN 'null';
 WHEN 'string' THEN RETURN p::text;
 WHEN 'object' THEN
  SELECT '{'||COALESCE(string_agg(to_jsonb(key)::text||':'||security_audit.canonical_json(value),',' ORDER BY key COLLATE "C"),'')||'}' INTO result FROM jsonb_each(p);
  RETURN result;
 WHEN 'array' THEN
  SELECT '['||COALESCE(string_agg(security_audit.canonical_json(value),',' ORDER BY ordinal),'')||']' INTO result FROM jsonb_array_elements(p) WITH ORDINALITY a(value,ordinal);
  RETURN result;
 ELSE RAISE EXCEPTION 'Unsupported canonical audit scalar';
 END CASE;
END $$;
ALTER FUNCTION security_audit.append_event(uuid,uuid,text,text) RENAME TO append_event_checked_binding;
CREATE FUNCTION security_audit.append_event(p_stream uuid,p_binding uuid,p_canonical text,p_digest text)
RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
DECLARE j jsonb; k text; v text;
BEGIN
 j:=p_canonical::jsonb;
 IF security_audit.has_duplicate_fields(p_canonical::json) THEN RAISE EXCEPTION 'Duplicate audit field'; END IF;
 IF p_canonical IS DISTINCT FROM security_audit.canonical_json(j) THEN RAISE EXCEPTION 'Noncanonical audit bytes'; END IF;
 FOREACH k IN ARRAY ARRAY['action','actorKind','applicationClientId','authenticationEvidence','correlationId','environmentId','eventAtUtc','eventId','operationId','outcome','previousEventSha256','reason','schemaVersion','scope','sequence','streamId','writerBindingReference'] LOOP
  IF jsonb_typeof(j->k) IS DISTINCT FROM 'string' THEN RAISE EXCEPTION 'Invalid closed scalar'; END IF;
 END LOOP;
 IF security_audit.valid_subject(j->'actor',true) IS NOT TRUE OR security_audit.valid_subject(j->'target',true) IS NOT TRUE OR
    security_audit.valid_subject(j->'writer',false) IS NOT TRUE OR
    jsonb_typeof(j->'eventAtUtc')<>'string' OR j->>'eventAtUtc' !~ '^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}\.[0-9]{7}Z$' OR
    jsonb_typeof(j->'sequence')<>'string' OR j->>'sequence' !~ '^[1-9][0-9]*$' OR
    (j->'securityVersion'<>'null'::jsonb AND (jsonb_typeof(j->'securityVersion')<>'string' OR j->>'securityVersion' !~ '^[1-9][0-9]*$')) OR
    (j->'customerId'='null'::jsonb)<>(j->'projectId'='null'::jsonb) OR
    (j->'customerId'='null'::jsonb AND j->>'scope'<>'Platform') OR
    (j->'customerId'<>'null'::jsonb AND (j->>'scope'<>'CustomerProject' OR j->>'action'<>'AuthorityChanged'))
    THEN RAISE EXCEPTION 'Closed event value refused'; END IF;
 IF j->'securityVersion'<>'null'::jsonb THEN PERFORM (j->>'securityVersion')::bigint; END IF;
 FOREACH k IN ARRAY ARRAY['eventId','operationId','correlationId','sessionReference','previousSessionReference','customerId','projectId'] LOOP
  v:=j->>k;
  IF j->k<>'null'::jsonb AND (jsonb_typeof(j->k)<>'string' OR v !~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' OR v='00000000-0000-0000-0000-000000000000')
  OR k IN ('eventId','operationId','correlationId') AND v IS NULL THEN RAISE EXCEPTION 'Invalid opaque identifier'; END IF;
 END LOOP;
 IF j->>'action' IN ('SessionIssued','SessionRevoked','SessionRotated','SubjectRevoked','AuthorityChanged') AND
    (j->>'outcome'<>'Succeeded' OR j->>'reason'<>'None' OR j->'target'='null'::jsonb OR j->'securityVersion'='null'::jsonb) OR
    j->>'action' IN ('SessionIssued','SessionRevoked','SessionRotated') AND j->'sessionReference'='null'::jsonb OR
    j->>'action'='SessionRotated' AND j->'previousSessionReference'='null'::jsonb OR
    j->>'action'<>'SessionRotated' AND j->'previousSessionReference'<>'null'::jsonb OR
    j->>'action'='AuthenticationDenied' AND j->>'outcome'<>'Denied' OR
    j->>'action'='AuthenticationFailed' AND j->>'outcome'<>'Failed' THEN RAISE EXCEPTION 'Invalid event combination'; END IF;
 PERFORM security_audit.append_event_checked_binding(p_stream,p_binding,p_canonical,p_digest);
END $$;
ALTER FUNCTION security_audit.append_receipt(uuid,text,text,uuid[]) RENAME TO append_receipt_checked_binding;
CREATE FUNCTION security_audit.append_receipt(p_operation uuid,p_request text,p_receipt text,p_events uuid[])
RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
DECLARE j jsonb; r jsonb; q jsonb; k text; v text;
BEGIN
 IF p_operation IS NULL OR p_operation='00000000-0000-0000-0000-000000000000' OR p_events IS NULL OR cardinality(p_events)=0 OR p_request IS NULL OR p_receipt IS NULL THEN RAISE EXCEPTION 'Missing receipt binding'; END IF;
 j:=p_request::jsonb; r:=p_receipt::jsonb; q:=r->'request';
 IF security_audit.has_duplicate_fields(p_request::json) OR security_audit.has_duplicate_fields(p_receipt::json) OR
    jsonb_typeof(j) IS DISTINCT FROM 'object' OR jsonb_typeof(r) IS DISTINCT FROM 'object' OR q IS DISTINCT FROM j THEN RAISE EXCEPTION 'Receipt duplicate/binding conflict'; END IF;
 IF p_request IS DISTINCT FROM security_audit.canonical_json(j) OR p_receipt IS DISTINCT FROM security_audit.canonical_json(r) THEN RAISE EXCEPTION 'Noncanonical receipt bytes'; END IF;
 IF (SELECT count(*) FROM jsonb_object_keys(j))<>13 OR NOT j ?& ARRAY['schemaVersion','operationId','issuer','actorKind','actor','operationKind','target','commandSha256','customerId','projectId','environmentId','assessmentId','oldSessionReference'] OR
    (SELECT count(*) FROM jsonb_object_keys(r))<>8 OR NOT r ?& ARRAY['schemaVersion','request','outcome','committedAtUtc','resultingRevision','securityVersion','newSessionReference','eventIds'] OR
    j->>'schemaVersion' IS DISTINCT FROM 'operation-receipt-request-v1' OR r->>'schemaVersion' IS DISTINCT FROM 'operation-receipt-v1' OR
    security_audit.valid_subject(j->'issuer',false) IS NOT TRUE OR security_audit.valid_subject(j->'target',false) IS NOT TRUE OR security_audit.valid_subject(j->'actor',true) IS NOT TRUE THEN RAISE EXCEPTION 'Closed receipt shape refused'; END IF;
 FOREACH k IN ARRAY ARRAY['schemaVersion','operationId','actorKind','operationKind','commandSha256'] LOOP
  IF jsonb_typeof(j->k) IS DISTINCT FROM 'string' THEN RAISE EXCEPTION 'Invalid request scalar'; END IF;
 END LOOP;
 FOREACH k IN ARRAY ARRAY['schemaVersion','outcome','committedAtUtc','securityVersion'] LOOP
  IF jsonb_typeof(r->k) IS DISTINCT FROM 'string' THEN RAISE EXCEPTION 'Invalid result scalar'; END IF;
 END LOOP;
 IF j->>'operationId' IS DISTINCT FROM p_operation::text OR j->>'commandSha256' !~ '^[0-9a-f]{64}$' OR
    j->>'actorKind' NOT IN ('Human','Workload','Anonymous') OR (j->>'actorKind'='Anonymous')<>(j->'actor'='null'::jsonb) OR
    j->>'operationKind' NOT IN ('SessionIssued','AuthenticationDenied','AuthenticationFailed','SessionRevoked','SessionRotated','SubjectRevoked','AuthorityChanged') OR
    r->>'outcome' NOT IN ('Succeeded','Denied','Failed') OR r->>'securityVersion' !~ '^[1-9][0-9]*$' OR
    (r->'resultingRevision'<>'null'::jsonb AND (jsonb_typeof(r->'resultingRevision')<>'string' OR r->>'resultingRevision' !~ '^[1-9][0-9]*$')) OR
    r->>'committedAtUtc' !~ '^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}\.[0-9]{7}Z$' OR
    jsonb_typeof(r->'eventIds') IS DISTINCT FROM 'array' OR
    (SELECT array_agg(x::uuid) FROM jsonb_array_elements_text(r->'eventIds') x) IS DISTINCT FROM p_events THEN RAISE EXCEPTION 'Receipt value refused'; END IF;
 PERFORM (r->>'securityVersion')::bigint; IF r->'resultingRevision'<>'null'::jsonb THEN PERFORM (r->>'resultingRevision')::bigint; END IF;
 FOREACH k IN ARRAY ARRAY['assessmentId','customerId','environmentId','oldSessionReference','projectId'] LOOP
  v:=j->>k;
  IF j->k<>'null'::jsonb AND (jsonb_typeof(j->k) IS DISTINCT FROM 'string' OR v !~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' OR v='00000000-0000-0000-0000-000000000000') THEN RAISE EXCEPTION 'Receipt scope invalid'; END IF;
 END LOOP;
 IF (j->'customerId'='null'::jsonb)<>(j->'projectId'='null'::jsonb) OR (j->'environmentId'='null'::jsonb)<>(j->'assessmentId'='null'::jsonb) OR
    (j->'environmentId'<>'null'::jsonb AND j->'customerId'='null'::jsonb) THEN RAISE EXCEPTION 'Receipt scope incomplete'; END IF;
 IF EXISTS(SELECT 1 FROM security_audit.events e WHERE e.event_id=ANY(p_events) AND
     (e.canonical_event::jsonb->'target' IS DISTINCT FROM j->'target' OR e.canonical_event::jsonb->>'action' IS DISTINCT FROM j->>'operationKind' OR
      e.canonical_event::jsonb->'actor' IS DISTINCT FROM j->'actor' OR e.canonical_event::jsonb->>'actorKind' IS DISTINCT FROM j->>'actorKind' OR
      e.canonical_event::jsonb->>'securityVersion' IS DISTINCT FROM r->>'securityVersion' OR e.canonical_event::jsonb->>'outcome' IS DISTINCT FROM r->>'outcome'))
    THEN RAISE EXCEPTION 'Receipt event conflict'; END IF;
 IF j->>'operationKind' IN ('SessionIssued','SessionRotated') THEN
  IF jsonb_typeof(r->'newSessionReference') IS DISTINCT FROM 'string' OR NOT EXISTS(SELECT 1 FROM security_audit.events WHERE event_id=ANY(p_events) AND canonical_event::jsonb->>'sessionReference'=r->>'newSessionReference') THEN RAISE EXCEPTION 'Receipt new reference mismatch'; END IF;
 ELSIF r->'newSessionReference' IS DISTINCT FROM 'null'::jsonb THEN RAISE EXCEPTION 'Unexpected receipt new reference'; END IF;
 IF j->>'operationKind' IN ('SessionRevoked','SessionRotated') AND (j->'oldSessionReference'='null'::jsonb OR NOT EXISTS(SELECT 1 FROM security_audit.events WHERE event_id=ANY(p_events) AND
   (j->>'operationKind'='SessionRevoked' AND canonical_event::jsonb->>'sessionReference'=j->>'oldSessionReference' OR j->>'operationKind'='SessionRotated' AND canonical_event::jsonb->>'previousSessionReference'=j->>'oldSessionReference'))) THEN RAISE EXCEPTION 'Exact old reference missing/conflicting'; END IF;
 IF EXISTS(SELECT 1 FROM security_audit.events WHERE event_id=ANY(p_events) AND (canonical_event::jsonb->'customerId' IS DISTINCT FROM j->'customerId' OR canonical_event::jsonb->'projectId' IS DISTINCT FROM j->'projectId' OR (canonical_event::jsonb->>'eventAtUtc')::timestamptz>(r->>'committedAtUtc')::timestamptz)) THEN RAISE EXCEPTION 'Receipt scope/time conflict'; END IF;
 PERFORM (r->>'committedAtUtc')::timestamptz;
 IF (r->>'committedAtUtc')::timestamptz>clock_timestamp() THEN RAISE EXCEPTION 'Untrusted receipt time'; END IF;
 PERFORM security_audit.append_receipt_checked_binding(p_operation,p_request,p_receipt,p_events);
END $$;
REVOKE ALL ON ALL FUNCTIONS IN SCHEMA security_audit FROM PUBLIC;

CREATE FUNCTION security_audit.revoke_reference(p_tenant uuid,p_object uuid,p_reference uuid,p_now timestamptz,p_operation uuid)
RETURNS boolean LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
BEGIN
 IF p_operation IS NULL OR p_operation='00000000-0000-0000-0000-000000000000' THEN RAISE EXCEPTION 'Atomic operation required'; END IF;
 UPDATE identity_sessions.tickets SET revoked_at=p_now,revoke_operation_id=p_operation WHERE tenant_id=p_tenant AND object_id=p_object AND session_reference=p_reference AND revoked_at IS NULL;
 RETURN FOUND;
END $$;
CREATE TABLE security_audit.subject_mutations (
 operation_id uuid PRIMARY KEY REFERENCES security_audit.operation_receipts(operation_id) DEFERRABLE INITIALLY DEFERRED,
 tenant_id uuid NOT NULL,object_id uuid NOT NULL,resulting_version bigint NOT NULL
);
CREATE FUNCTION security_audit.revoke_subject(p_tenant uuid,p_object uuid,p_disable boolean,p_now timestamptz,p_operation uuid)
RETURNS bigint LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
DECLARE n bigint;
BEGIN
 IF p_operation IS NULL OR p_operation='00000000-0000-0000-0000-000000000000' THEN RAISE EXCEPTION 'Atomic operation required'; END IF;
 UPDATE identity_sessions.subjects SET security_version=security_version+1,active=CASE WHEN p_disable THEN false ELSE active END
 WHERE tenant_id=p_tenant AND object_id=p_object RETURNING security_version INTO STRICT n;
 UPDATE identity_sessions.tickets SET revoked_at=p_now,revoke_operation_id=p_operation WHERE tenant_id=p_tenant AND object_id=p_object AND revoked_at IS NULL;
 INSERT INTO security_audit.subject_mutations VALUES(p_operation,p_tenant,p_object,n);
 RETURN n;
END $$;
CREATE FUNCTION security_audit.check_subject_audit()
RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
BEGIN
 IF NOT EXISTS(SELECT 1 FROM security_audit.events e JOIN security_audit.operation_receipts r USING(operation_id)
 WHERE e.operation_id=NEW.operation_id AND e.event_id=ANY(r.event_ids) AND e.canonical_event::jsonb->>'action'='SubjectRevoked'
 AND e.canonical_event::jsonb->>'securityVersion'=NEW.resulting_version::text
 AND e.canonical_event::jsonb->'target'=jsonb_build_object('tenantId',NEW.tenant_id::text,'objectId',NEW.object_id::text))
 THEN RAISE EXCEPTION 'Subject mutation lacks matching audit'; END IF;
 RETURN NULL;
END $$;
CREATE CONSTRAINT TRIGGER subject_requires_atomic_audit AFTER INSERT ON security_audit.subject_mutations
DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION security_audit.check_subject_audit();
REVOKE ALL ON ALL TABLES IN SCHEMA security_audit FROM PUBLIC;
REVOKE ALL ON ALL FUNCTIONS IN SCHEMA security_audit FROM PUBLIC;

CREATE FUNCTION security_audit.check_mutation_event_receipt()
RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
BEGIN
 IF NEW.canonical_event::jsonb->>'action' IN ('SessionIssued','SessionRevoked','SessionRotated','SubjectRevoked','AuthorityChanged') AND
   NOT EXISTS(SELECT 1 FROM security_audit.operation_receipts WHERE operation_id=NEW.operation_id AND NEW.event_id=ANY(event_ids))
 THEN RAISE EXCEPTION 'Mutation event requires atomic durable receipt'; END IF;
 RETURN NULL;
END $$;
CREATE CONSTRAINT TRIGGER event_requires_atomic_receipt AFTER INSERT ON security_audit.events
DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION security_audit.check_mutation_event_receipt();
REVOKE ALL ON ALL FUNCTIONS IN SCHEMA security_audit FROM PUBLIC;

CREATE FUNCTION security_audit.lookup_ticket_subject(p_hash text)
RETURNS TABLE(tenant_id uuid,object_id uuid) LANGUAGE sql SECURITY DEFINER SET search_path=pg_catalog AS $$
 SELECT t.tenant_id,t.object_id FROM identity_sessions.tickets t WHERE t.key_hash=p_hash
$$;
REVOKE ALL ON ALL FUNCTIONS IN SCHEMA security_audit FROM PUBLIC;
