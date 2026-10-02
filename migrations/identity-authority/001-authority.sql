-- Additive synthetic authority schema. Explicit migration identity only; no startup migration or live grants.
CREATE SCHEMA identity_authority;
REVOKE ALL ON SCHEMA identity_authority FROM PUBLIC;
CREATE TABLE identity_authority.writer_bindings (
    login_name name PRIMARY KEY, kind text NOT NULL CHECK(kind IN ('Administrator','Provider','Reader')),
    tenant_id uuid NOT NULL, object_id uuid NOT NULL,
    CHECK(tenant_id <> '00000000-0000-0000-0000-000000000000' AND object_id <> '00000000-0000-0000-0000-000000000000')
);
CREATE TABLE identity_authority.customers (customer_id uuid PRIMARY KEY);
CREATE TABLE identity_authority.projects (customer_id uuid NOT NULL REFERENCES identity_authority.customers, project_id uuid NOT NULL UNIQUE, PRIMARY KEY(customer_id,project_id));
CREATE TABLE identity_authority.environments (customer_id uuid NOT NULL, project_id uuid NOT NULL, environment_id uuid NOT NULL UNIQUE,
    PRIMARY KEY(customer_id,project_id,environment_id), FOREIGN KEY(customer_id,project_id) REFERENCES identity_authority.projects);
CREATE TABLE identity_authority.assessments (customer_id uuid NOT NULL, project_id uuid NOT NULL, environment_id uuid NOT NULL, assessment_id uuid NOT NULL UNIQUE,
    PRIMARY KEY(customer_id,project_id,environment_id,assessment_id), FOREIGN KEY(customer_id,project_id,environment_id) REFERENCES identity_authority.environments);
CREATE TABLE identity_authority.enrollments (
    tenant_id uuid NOT NULL, object_id uuid NOT NULL, revision bigint NOT NULL CHECK(revision>0), lifecycle text NOT NULL CHECK(lifecycle IN ('Pending','Active','Suspended','Revoked')),
    origin text NOT NULL CHECK(origin IN ('InternalOrganizational','ExternalOrganizational')), home_tenant_id uuid NOT NULL,
    payload jsonb NOT NULL, PRIMARY KEY(tenant_id,object_id), FOREIGN KEY(tenant_id,object_id) REFERENCES identity_sessions.subjects,
    CHECK(home_tenant_id <> '00000000-0000-0000-0000-000000000000' AND home_tenant_id <> '9188040d-6c67-4c5b-b112-36a304b66dad'),
    CHECK(origin <> 'InternalOrganizational' OR home_tenant_id=tenant_id)
);
CREATE TABLE identity_authority.assignments (
    assignment_id uuid PRIMARY KEY, tenant_id uuid NOT NULL, object_id uuid NOT NULL, customer_id uuid NOT NULL, project_id uuid NOT NULL, environment_id uuid NOT NULL, assessment_id uuid NOT NULL,
    role text NOT NULL CHECK(role IN ('Consultant','CustomerReviewer','EvidenceAuthorizer','CustomerRiskOwner','Executive','Auditor','PlatformSupport')),
    revision bigint NOT NULL CHECK(revision>0), payload jsonb NOT NULL,
    UNIQUE(tenant_id,object_id,customer_id,project_id,environment_id,assessment_id,role),
    FOREIGN KEY(tenant_id,object_id) REFERENCES identity_authority.enrollments,
    FOREIGN KEY(customer_id,project_id,environment_id,assessment_id) REFERENCES identity_authority.assessments
);
CREATE TABLE identity_authority.guests (tenant_id uuid NOT NULL, object_id uuid NOT NULL, payload jsonb NOT NULL, PRIMARY KEY(tenant_id,object_id), FOREIGN KEY(tenant_id,object_id) REFERENCES identity_authority.enrollments);
CREATE TABLE identity_authority.role_bindings (tenant_id uuid PRIMARY KEY, payload jsonb NOT NULL);
CREATE TABLE identity_authority.provider_observations (tenant_id uuid NOT NULL, object_id uuid NOT NULL, sequence bigint NOT NULL CHECK(sequence>0), started_at timestamptz NOT NULL,
    cutoff timestamptz NOT NULL, payload jsonb NOT NULL, home_payload jsonb, published_security_version bigint NOT NULL,
    PRIMARY KEY(tenant_id,object_id), FOREIGN KEY(tenant_id,object_id) REFERENCES identity_authority.enrollments);
CREATE TABLE identity_authority.mutations (operation_id uuid PRIMARY KEY, tenant_id uuid NOT NULL, object_id uuid NOT NULL, command_sha256 char(64) NOT NULL,
    revision bigint NOT NULL, security_version bigint NOT NULL, command_payload jsonb NOT NULL,
    FOREIGN KEY(operation_id) REFERENCES security_audit.operation_receipts(operation_id) DEFERRABLE INITIALLY DEFERRED);
REVOKE ALL ON ALL TABLES IN SCHEMA identity_authority FROM PUBLIC;

CREATE FUNCTION identity_authority.require_writer(p_kind text,p_tenant uuid) RETURNS identity_authority.writer_bindings
LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
DECLARE w identity_authority.writer_bindings;
BEGIN
    SELECT * INTO w FROM identity_authority.writer_bindings WHERE login_name=session_user AND tenant_id=p_tenant;
    IF NOT FOUND OR (w.kind<>p_kind AND p_kind<>'Reader') THEN RAISE EXCEPTION 'authority writer denied' USING ERRCODE='42501'; END IF;
    RETURN w;
END $$;
CREATE FUNCTION identity_authority.lock_subject(p_tenant uuid,p_object uuid) RETURNS jsonb
LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
DECLARE e identity_authority.enrollments; v bigint; active_state boolean;
BEGIN
    PERFORM identity_authority.require_writer('Reader',p_tenant);
    PERFORM pg_catalog.pg_advisory_xact_lock(pg_catalog.hashtextextended(p_tenant::text||':'||p_object::text,0));
    SELECT security_version,active INTO v,active_state FROM identity_sessions.subjects WHERE tenant_id=p_tenant AND object_id=p_object FOR UPDATE;
    SELECT * INTO e FROM identity_authority.enrollments WHERE tenant_id=p_tenant AND object_id=p_object;
    IF NOT FOUND THEN RETURN NULL; END IF;
    RETURN jsonb_build_object('enrollment',e.payload,'securityVersion',v,'assignments',COALESCE((SELECT jsonb_agg(payload ORDER BY assignment_id) FROM identity_authority.assignments WHERE tenant_id=p_tenant AND object_id=p_object),'[]'::jsonb),
        'guest',(SELECT payload FROM identity_authority.guests WHERE tenant_id=p_tenant AND object_id=p_object),
        'provider',(SELECT payload FROM identity_authority.provider_observations WHERE tenant_id=p_tenant AND object_id=p_object),
        'home',(SELECT home_payload FROM identity_authority.provider_observations WHERE tenant_id=p_tenant AND object_id=p_object),
        'providerStatusValid',active_state,'providerPublishedSecurityVersion',(SELECT published_security_version FROM identity_authority.provider_observations WHERE tenant_id=p_tenant AND object_id=p_object),
        'sponsorActive',EXISTS(SELECT 1 FROM identity_authority.guests g JOIN identity_authority.enrollments se ON se.tenant_id=(g.payload->'sponsor'->>'tenantId')::uuid AND se.object_id=(g.payload->'sponsor'->>'objectId')::uuid WHERE g.tenant_id=p_tenant AND g.object_id=p_object AND se.lifecycle='Active'));
END $$;
CREATE FUNCTION identity_authority.closed(p jsonb,keys text[]) RETURNS void LANGUAGE plpgsql SET search_path=pg_catalog AS $$
BEGIN
    IF p IS NULL OR jsonb_typeof(p) IS DISTINCT FROM 'object' OR EXISTS(SELECT 1 FROM jsonb_object_keys(p) k WHERE NOT(k=ANY(keys))) OR EXISTS(SELECT 1 FROM unnest(keys) k WHERE NOT(p ? k)) THEN RAISE EXCEPTION 'closed authority schema denied'; END IF;
END $$;
CREATE FUNCTION identity_authority.valid_subject(p jsonb) RETURNS boolean LANGUAGE plpgsql SET search_path=pg_catalog AS $$
BEGIN
    PERFORM identity_authority.closed(p,ARRAY['tenantId','objectId']);
    RETURN COALESCE(p->>'tenantId' ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
        AND p->>'objectId' ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
        AND (p->>'tenantId')::uuid NOT IN ('00000000-0000-0000-0000-000000000000','9188040d-6c67-4c5b-b112-36a304b66dad')
        AND (p->>'objectId')::uuid<>'00000000-0000-0000-0000-000000000000',false);
END $$;
CREATE FUNCTION identity_authority.valid_attribution(p jsonb,d jsonb) RETURNS boolean LANGUAGE plpgsql SET search_path=pg_catalog AS $$
BEGIN
    PERFORM identity_authority.closed(p,ARRAY['approvedDecisionId','approvedBySubject','approvedAtUtc']);
    RETURN COALESCE(identity_authority.valid_subject(p->'approvedBySubject') AND p->'approvedBySubject'=d->'administrator'
        AND p->>'approvedDecisionId'=d->>'decisionReference' AND p->>'approvedAtUtc' ~ '^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z$'
        AND (p->>'approvedAtUtc')::timestamptz<=clock_timestamp(),false);
END $$;
CREATE FUNCTION identity_authority.typed(p jsonb,fields text[],kind text) RETURNS void LANGUAGE plpgsql SET search_path=pg_catalog AS $$
DECLARE field text;
BEGIN
    FOREACH field IN ARRAY fields LOOP
        IF jsonb_typeof(p->field) IS DISTINCT FROM kind THEN RAISE EXCEPTION 'authority field type denied'; END IF;
    END LOOP;
END $$;
CREATE FUNCTION identity_authority.utc(p jsonb,fields text[]) RETURNS void LANGUAGE plpgsql SET search_path=pg_catalog AS $$
DECLARE field text;
BEGIN
    PERFORM identity_authority.typed(p,fields,'string');
    FOREACH field IN ARRAY fields LOOP
        IF p->>field !~ '^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z$' THEN RAISE EXCEPTION 'canonical authority UTC denied'; END IF;
        PERFORM (p->>field)::timestamptz;
    END LOOP;
END $$;
CREATE FUNCTION identity_authority.ids(p jsonb,fields text[]) RETURNS void LANGUAGE plpgsql SET search_path=pg_catalog AS $$
DECLARE field text;
BEGIN
    PERFORM identity_authority.typed(p,fields,'string');
    FOREACH field IN ARRAY fields LOOP
        IF p->>field !~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
            OR (p->>field)::uuid='00000000-0000-0000-0000-000000000000' THEN RAISE EXCEPTION 'canonical authority identifier denied'; END IF;
    END LOOP;
END $$;
CREATE FUNCTION identity_authority.apply_command(p jsonb) RETURNS TABLE(revision bigint,security_version bigint)
LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
DECLARE d jsonb; target jsonb; en jsonb; a jsonb; g jsonb; t uuid; o uuid; w identity_authority.writer_bindings;
    current_en identity_authority.enrollments; current_assignment identity_authority.assignments; op text; nr bigint; nv bigint; now_at timestamptz;
BEGIN
    PERFORM identity_authority.closed(p,ARRAY['schemaVersion','decision','enrollment','assignment','guest']);
    PERFORM identity_authority.typed(p,ARRAY['schemaVersion'],'string');
    d=p->'decision'; PERFORM identity_authority.closed(d,ARRAY['schemaVersion','commandId','expectedRevision','subject','scope','operation','decisionReference','reason','administrator','payloadSha256']);
    PERFORM identity_authority.typed(d,ARRAY['schemaVersion','operation','reason','payloadSha256'],'string');
    PERFORM identity_authority.typed(d,ARRAY['expectedRevision'],'number');
    PERFORM identity_authority.ids(d,ARRAY['commandId','decisionReference']);
    IF d->>'expectedRevision' !~ '^(0|[1-9][0-9]*)$' THEN RAISE EXCEPTION 'authority integer revision denied'; END IF;
    target=d->'subject'; PERFORM identity_authority.closed(target,ARRAY['tenantId','objectId']);
    IF NOT identity_authority.valid_subject(target) OR NOT identity_authority.valid_subject(d->'administrator') THEN RAISE EXCEPTION 'authority subject denied'; END IF;
    t=(target->>'tenantId')::uuid; o=(target->>'objectId')::uuid; w=identity_authority.require_writer('Administrator',t);
    IF p->>'schemaVersion'<>'authority-command-v1' OR d->>'schemaVersion'<>'authority-decision-v1'
        OR (d->'administrator'->>'tenantId')::uuid<>w.tenant_id OR (d->'administrator'->>'objectId')::uuid<>w.object_id
        OR d->>'payloadSha256' !~ '^[0-9a-f]{64}$' OR (d->>'commandId')::uuid='00000000-0000-0000-0000-000000000000'
        OR (d->>'decisionReference')::uuid='00000000-0000-0000-0000-000000000000'
        OR d->>'reason' NOT IN ('None','ApprovedOnboarding','ApprovedAssignment','ApprovedRenewal','Incident','Termination','SponsorOrEngagementChanged') THEN RAISE EXCEPTION 'authority binding denied'; END IF;
    PERFORM identity_authority.lock_subject(t,o);
    SELECT * INTO current_en FROM identity_authority.enrollments WHERE tenant_id=t AND object_id=o;
    now_at=clock_timestamp(); op=d->>'operation'; en=p->'enrollment'; a=p->'assignment'; g=p->'guest';
    IF (en<>'null'::jsonb) IS DISTINCT FROM (op IN ('EnrollPending','ActivateEnrollment'))
        OR (a<>'null'::jsonb) IS DISTINCT FROM (op IN ('SetAssignment','RevokeAssignment'))
        OR (g<>'null'::jsonb) IS DISTINCT FROM (op='ApproveExternalLifecycle')
        OR (d->'scope'<>'null'::jsonb) IS DISTINCT FROM (op IN ('SetAssignment','RevokeAssignment')) THEN RAISE EXCEPTION 'authority operation payload denied'; END IF;
    IF en<>'null'::jsonb THEN
        PERFORM identity_authority.closed(en,ARRAY['schemaVersion','subject','revision','lifecycle','origin','homeTenantId','enrolledAtUtc','attribution']);
        PERFORM identity_authority.typed(en,ARRAY['schemaVersion','lifecycle','origin'],'string');
        PERFORM identity_authority.typed(en,ARRAY['revision'],'number');
        PERFORM identity_authority.ids(en,ARRAY['homeTenantId']); PERFORM identity_authority.utc(en,ARRAY['enrolledAtUtc']);
        IF NOT identity_authority.valid_subject(en->'subject') THEN RAISE EXCEPTION 'enrollment subject denied'; END IF;
    END IF;
    IF op='EnrollPending' THEN
        PERFORM identity_authority.closed(en,ARRAY['schemaVersion','subject','revision','lifecycle','origin','homeTenantId','enrolledAtUtc','attribution']);
        IF NOT identity_authority.valid_attribution(en->'attribution',d) THEN RAISE EXCEPTION 'enrollment attribution denied'; END IF;
        IF current_en.tenant_id IS NOT NULL OR (d->>'expectedRevision')::bigint<>0 OR en->>'lifecycle'<>'Pending' OR (en->>'revision')::bigint<>1 THEN RAISE EXCEPTION 'enrollment conflict'; END IF;
        IF en->>'schemaVersion'<>'subject-enrollment-v1' OR en->'subject'<>target OR en->>'origin' NOT IN ('InternalOrganizational','ExternalOrganizational')
            OR (en->>'enrolledAtUtc')::timestamptz>now_at OR (en->'attribution'->>'approvedAtUtc')::timestamptz>now_at THEN RAISE EXCEPTION 'enrollment denied'; END IF;
        INSERT INTO identity_sessions.subjects(tenant_id,object_id,active,security_version,provider_checked_at,sign_in_valid_from_at) VALUES(t,o,false,1,'epoch',NULL);
        INSERT INTO identity_authority.enrollments VALUES(t,o,1,'Pending',en->>'origin',(en->>'homeTenantId')::uuid,en);
        nr=1; nv=1;
    ELSE
        IF current_en.tenant_id IS NULL OR current_en.lifecycle='Revoked' OR current_en.revision<>(d->>'expectedRevision')::bigint THEN RAISE EXCEPTION 'authority revision denied'; END IF;
        nr=current_en.revision+1;
        SELECT s.security_version+1 INTO nv FROM identity_sessions.subjects s WHERE s.tenant_id=t AND s.object_id=o;
        IF op='ActivateEnrollment' THEN
            PERFORM identity_authority.closed(en,ARRAY['schemaVersion','subject','revision','lifecycle','origin','homeTenantId','enrolledAtUtc','attribution']);
            IF en->>'schemaVersion'<>'subject-enrollment-v1' OR en->'subject'<>target OR en->>'lifecycle'<>'Active' OR (en->>'revision')::bigint<>nr
                OR en->>'origin'<>current_en.origin OR (en->>'homeTenantId')::uuid<>current_en.home_tenant_id
                OR en->>'enrolledAtUtc'<>current_en.payload->>'enrolledAtUtc' OR en->'attribution'<>current_en.payload->'attribution' THEN RAISE EXCEPTION 'immutable enrollment binding denied'; END IF;
            current_en.lifecycle='Active';
        ELSIF op IN ('SuspendSubject','RevokeSubject') THEN current_en.lifecycle=CASE WHEN op='RevokeSubject' THEN 'Revoked' ELSE 'Suspended' END;
        ELSIF op IN ('SetAssignment','RevokeAssignment') THEN
            PERFORM identity_authority.closed(a,ARRAY['schemaVersion','assignmentId','subject','scope','role','active','startsAtUtc','expiresAtUtc','evidenceCategories','conditions','revision','attribution']);
            PERFORM identity_authority.closed(a->'scope',ARRAY['customerId','projectId','environmentId','assessmentId']);
            PERFORM identity_authority.ids(a,ARRAY['assignmentId']); PERFORM identity_authority.ids(a->'scope',ARRAY['customerId','projectId','environmentId','assessmentId']);
            PERFORM identity_authority.typed(a,ARRAY['schemaVersion','role'],'string'); PERFORM identity_authority.typed(a,ARRAY['revision'],'number');
            PERFORM identity_authority.typed(a,ARRAY['active'],'boolean'); PERFORM identity_authority.typed(a,ARRAY['evidenceCategories','conditions'],'array');
            PERFORM identity_authority.utc(a,ARRAY['startsAtUtc','expiresAtUtc']);
            IF NOT identity_authority.valid_attribution(a->'attribution',d) THEN RAISE EXCEPTION 'assignment attribution denied'; END IF;
            IF a->>'schemaVersion'<>'human-assignment-v1' OR a->'subject'<>target OR a->'scope'<>d->'scope'
                OR jsonb_typeof(a->'active')<>'boolean' OR (op='RevokeAssignment' AND (a->>'active')::boolean)
                OR (a->>'expiresAtUtc')::timestamptz<=(a->>'startsAtUtc')::timestamptz
                OR jsonb_typeof(a->'evidenceCategories')<>'array' OR jsonb_array_length(a->'evidenceCategories')=0
                OR jsonb_typeof(a->'conditions')<>'array' THEN RAISE EXCEPTION 'assignment denied'; END IF;
            IF EXISTS(SELECT 1 FROM jsonb_array_elements(a->'evidenceCategories') k WHERE jsonb_typeof(k)<>'string')
                OR EXISTS(SELECT 1 FROM jsonb_array_elements(a->'conditions') k WHERE jsonb_typeof(k)<>'string')
                OR EXISTS(SELECT 1 FROM jsonb_array_elements_text(a->'evidenceCategories') k WHERE k !~ '^[A-Za-z0-9_.-]{1,128}$')
                OR (SELECT count(*) FROM jsonb_array_elements_text(a->'evidenceCategories'))<>(SELECT count(DISTINCT value) FROM jsonb_array_elements_text(a->'evidenceCategories'))
                OR EXISTS(SELECT 1 FROM jsonb_array_elements_text(a->'conditions') k WHERE k NOT IN ('SeparateViewer','CustomerProtectedAuthorization','DedicatedProtectedPermission','ReviewerEditPermission','RiskRequiredFields','AuditedOverride','ExplicitExportGrant','PublishWarningAcknowledged','ExecutiveAcknowledgmentAuthority','ExplicitDeletionPermission','SeparateCustomerAdministrator','ApprovedReportProvenance','SupportMetadataPolicy'))
                OR (SELECT count(*) FROM jsonb_array_elements_text(a->'conditions'))<>(SELECT count(DISTINCT value) FROM jsonb_array_elements_text(a->'conditions')) THEN RAISE EXCEPTION 'assignment grants denied'; END IF;
            SELECT * INTO current_assignment FROM identity_authority.assignments WHERE assignment_id=(a->>'assignmentId')::uuid;
            IF FOUND THEN
                IF current_assignment.tenant_id<>t OR current_assignment.object_id<>o OR current_assignment.payload->'scope'<>a->'scope'
                    OR current_assignment.role<>a->>'role' OR current_assignment.revision+1<>(a->>'revision')::bigint THEN RAISE EXCEPTION 'assignment target denied'; END IF;
                UPDATE identity_authority.assignments SET payload=a,revision=(a->>'revision')::bigint WHERE assignment_id=current_assignment.assignment_id;
            ELSE
                IF (a->>'revision')::bigint<>1 OR op='RevokeAssignment' THEN RAISE EXCEPTION 'assignment revision denied'; END IF;
                INSERT INTO identity_authority.assignments VALUES((a->>'assignmentId')::uuid,t,o,(a->'scope'->>'customerId')::uuid,(a->'scope'->>'projectId')::uuid,(a->'scope'->>'environmentId')::uuid,(a->'scope'->>'assessmentId')::uuid,a->>'role',1,a);
            END IF;
        ELSIF op='ApproveExternalLifecycle' THEN
            PERFORM identity_authority.closed(g,ARRAY['schemaVersion','subject','sponsor','assignedAtUtc','expiresAtUtc','lastReviewedAtUtc','engagementReference','engagementRevision','sponsorOrEngagementChanged','attribution']);
            PERFORM identity_authority.ids(g,ARRAY['engagementReference']); PERFORM identity_authority.typed(g,ARRAY['engagementRevision'],'number');
            PERFORM identity_authority.typed(g,ARRAY['sponsorOrEngagementChanged'],'boolean'); PERFORM identity_authority.typed(g,ARRAY['schemaVersion'],'string');
            PERFORM identity_authority.utc(g,ARRAY['assignedAtUtc','expiresAtUtc','lastReviewedAtUtc']);
            IF (g->>'engagementRevision')::bigint<=0 THEN RAISE EXCEPTION 'guest engagement revision denied'; END IF;
            IF NOT identity_authority.valid_attribution(g->'attribution',d) OR NOT identity_authority.valid_subject(g->'sponsor') OR g->'sponsor'=target THEN RAISE EXCEPTION 'guest attribution denied'; END IF;
            IF current_en.origin<>'ExternalOrganizational' OR g->>'schemaVersion'<>'guest-lifecycle-v1' OR g->'subject'<>target
                OR (g->>'sponsorOrEngagementChanged')::boolean OR (g->>'assignedAtUtc')::timestamptz>now_at
                OR (g->>'lastReviewedAtUtc')::timestamptz>now_at OR (g->>'lastReviewedAtUtc')::timestamptz<(g->>'assignedAtUtc')::timestamptz
                OR now_at-(g->>'lastReviewedAtUtc')::timestamptz>interval '30 days'
                OR (g->>'expiresAtUtc')::timestamptz<=now_at OR (g->>'expiresAtUtc')::timestamptz>(g->>'assignedAtUtc')::timestamptz+interval '90 days'
                OR NOT EXISTS(SELECT 1 FROM identity_authority.enrollments se WHERE se.tenant_id=(g->'sponsor'->>'tenantId')::uuid AND se.object_id=(g->'sponsor'->>'objectId')::uuid AND se.lifecycle='Active') THEN RAISE EXCEPTION 'external lifecycle denied'; END IF;
            INSERT INTO identity_authority.guests VALUES(t,o,g) ON CONFLICT(tenant_id,object_id) DO UPDATE SET payload=EXCLUDED.payload;
        ELSIF op='MarkExternalChange' THEN
            IF current_en.origin<>'ExternalOrganizational' THEN RAISE EXCEPTION 'external target denied'; END IF;
            UPDATE identity_authority.guests SET payload=jsonb_set(payload,'{sponsorOrEngagementChanged}','true') WHERE tenant_id=t AND object_id=o;
        ELSE RAISE EXCEPTION 'unknown authority operation'; END IF;
        current_en.payload=jsonb_set(jsonb_set(current_en.payload,'{revision}',to_jsonb(nr)),'{lifecycle}',to_jsonb(current_en.lifecycle));
        UPDATE identity_authority.enrollments SET revision=nr,lifecycle=current_en.lifecycle,payload=current_en.payload WHERE tenant_id=t AND object_id=o;
        UPDATE identity_sessions.subjects SET security_version=nv,active=false,provider_checked_at='epoch' WHERE tenant_id=t AND object_id=o;
        UPDATE identity_sessions.tickets SET revoked_at=now_at,revoke_operation_id=(d->>'commandId')::uuid WHERE tenant_id=t AND object_id=o AND revoked_at IS NULL;
    END IF;
    INSERT INTO identity_authority.mutations VALUES((d->>'commandId')::uuid,t,o,d->>'payloadSha256',nr,nv,p);
    RETURN QUERY SELECT nr,nv;
END $$;

CREATE FUNCTION identity_authority.publish_provider(p jsonb,h jsonb,p_operation uuid,p_digest text) RETURNS TABLE(revision bigint,security_version bigint)
LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
DECLARE t uuid; o uuid; e identity_authority.enrollments; prev identity_authority.provider_observations; b jsonb; now_at timestamptz; start_at timestamptz; end_at timestamptz; cutoff_at timestamptz; v bigint; valid boolean; changed boolean;
BEGIN
    PERFORM identity_authority.closed(p,ARRAY['schemaVersion','subject','clientId','resourceServicePrincipalId','sequence','startedAtUtc','completedAtUtc','enrollmentRevision','securityVersion','returnedSubjectId','accountEnabled','userType','invitationState','resourceCutoffUtc','appRoleIds','complete','correlationId']);
    PERFORM identity_authority.typed(p,ARRAY['schemaVersion','userType'],'string'); PERFORM identity_authority.typed(p,ARRAY['sequence','enrollmentRevision','securityVersion'],'number');
    PERFORM identity_authority.typed(p,ARRAY['accountEnabled','complete'],'boolean'); PERFORM identity_authority.typed(p,ARRAY['appRoleIds'],'array');
    PERFORM identity_authority.ids(p,ARRAY['clientId','resourceServicePrincipalId','returnedSubjectId','correlationId']);
    PERFORM identity_authority.utc(p,ARRAY['startedAtUtc','completedAtUtc','resourceCutoffUtc']);
    IF EXISTS(SELECT 1 FROM jsonb_array_elements(p->'appRoleIds') k WHERE jsonb_typeof(k)<>'string')
        OR p->'appRoleIds' IS DISTINCT FROM COALESCE((SELECT jsonb_agg(value ORDER BY value) FROM jsonb_array_elements_text(p->'appRoleIds')),'[]'::jsonb) THEN RAISE EXCEPTION 'provider role order denied'; END IF;
    IF NOT identity_authority.valid_subject(p->'subject') OR p->'invitationState'<>'null'::jsonb AND (jsonb_typeof(p->'invitationState')<>'string' OR p->>'invitationState' NOT IN ('Accepted','PendingAcceptance'))
        OR p_operation='00000000-0000-0000-0000-000000000000' OR p_digest !~ '^[0-9a-f]{64}$' THEN RAISE EXCEPTION 'provider schema denied'; END IF;
    t=(p->'subject'->>'tenantId')::uuid; o=(p->'subject'->>'objectId')::uuid;
    PERFORM identity_authority.require_writer('Provider',t); PERFORM identity_authority.lock_subject(t,o);
    SELECT * INTO e FROM identity_authority.enrollments WHERE tenant_id=t AND object_id=o;
    SELECT s.security_version INTO v FROM identity_sessions.subjects s WHERE s.tenant_id=t AND s.object_id=o;
    SELECT payload INTO b FROM identity_authority.role_bindings WHERE tenant_id=t;
    SELECT * INTO prev FROM identity_authority.provider_observations WHERE tenant_id=t AND object_id=o;
    now_at=clock_timestamp(); start_at=(p->>'startedAtUtc')::timestamptz; end_at=(p->>'completedAtUtc')::timestamptz; cutoff_at=(p->>'resourceCutoffUtc')::timestamptz;
    IF e.tenant_id IS NULL OR e.lifecycle<>'Active' OR b IS NULL OR p->>'schemaVersion'<>'provider-observation-v1'
        OR (p->>'enrollmentRevision')::bigint<>e.revision OR (p->>'securityVersion')::bigint<>v
        OR (p->>'returnedSubjectId')::uuid<>o OR p->>'clientId'<>b->>'clientId' OR p->>'resourceServicePrincipalId'<>b->>'resourceServicePrincipalId'
        OR p->>'userType' NOT IN ('Member','Guest') OR jsonb_typeof(p->'accountEnabled')<>'boolean' OR p->'complete'<>'true'::jsonb
        OR (p->>'sequence')::bigint<1 OR start_at>end_at OR end_at>now_at OR now_at-start_at>=interval '15 minutes' OR cutoff_at>now_at
        OR jsonb_typeof(p->'appRoleIds')<>'array' OR EXISTS(SELECT 1 FROM jsonb_array_elements_text(p->'appRoleIds') r WHERE NOT EXISTS(SELECT 1 FROM jsonb_array_elements(b->'appRoles') br WHERE br->>'appRoleId'=r AND br->'enabled'='true'::jsonb))
        OR (SELECT count(*) FROM jsonb_array_elements_text(p->'appRoleIds'))<>(SELECT count(DISTINCT value) FROM jsonb_array_elements_text(p->'appRoleIds'))
        OR (prev.tenant_id IS NOT NULL AND ((p->>'sequence')::bigint<=prev.sequence OR start_at<prev.started_at OR cutoff_at<prev.cutoff)) THEN RAISE EXCEPTION 'provider observation denied'; END IF;
    valid=(p->>'accountEnabled')::boolean AND jsonb_array_length(p->'appRoleIds')>0;
    IF e.origin='ExternalOrganizational' THEN
        PERFORM identity_authority.closed(h,ARRAY['subject','homeTenantId','enrollmentRevision','securityVersion','checkedAtUtc','cutoffUtc','active','complete']);
        PERFORM identity_authority.ids(h,ARRAY['homeTenantId']); PERFORM identity_authority.utc(h,ARRAY['checkedAtUtc','cutoffUtc']);
        PERFORM identity_authority.typed(h,ARRAY['enrollmentRevision','securityVersion'],'number'); PERFORM identity_authority.typed(h,ARRAY['active','complete'],'boolean');
        valid=valid AND p->>'invitationState'='Accepted' AND h IS NOT NULL
            AND h->'subject'=p->'subject' AND (h->>'homeTenantId')::uuid=e.home_tenant_id
            AND (h->>'enrollmentRevision')::bigint=e.revision AND (h->>'securityVersion')::bigint=v
            AND h->'active'='true'::jsonb AND h->'complete'='true'::jsonb
            AND (h->>'checkedAtUtc')::timestamptz<=now_at AND now_at-(h->>'checkedAtUtc')::timestamptz<interval '15 minutes'
            AND (h->>'cutoffUtc')::timestamptz<=now_at;
        IF prev.home_payload IS NOT NULL AND ((h->>'cutoffUtc')::timestamptz<(prev.home_payload->>'cutoffUtc')::timestamptz
            OR (h->>'checkedAtUtc')::timestamptz<(prev.home_payload->>'checkedAtUtc')::timestamptz) THEN RAISE EXCEPTION 'home status regression denied'; END IF;
    ELSE IF h IS NOT NULL THEN RAISE EXCEPTION 'internal home evidence denied'; END IF; END IF;
    changed=prev.tenant_id IS NOT NULL AND (prev.payload->'appRoleIds'<>p->'appRoleIds' OR prev.payload->'accountEnabled'<>p->'accountEnabled' OR prev.payload->'userType'<>p->'userType' OR prev.payload->'invitationState' IS DISTINCT FROM p->'invitationState');
    IF changed OR NOT COALESCE(valid,false) THEN
        v=v+1; UPDATE identity_sessions.tickets SET revoked_at=now_at,revoke_operation_id=p_operation WHERE tenant_id=t AND object_id=o AND revoked_at IS NULL;
        -- Classification is not origin. Member/Guest transitions revoke old sessions,
        -- while independent reviewed origin/home/lifecycle evidence still governs admission.
    END IF;
    INSERT INTO identity_authority.provider_observations VALUES(t,o,(p->>'sequence')::bigint,start_at,cutoff_at,p,h,v)
        ON CONFLICT(tenant_id,object_id) DO UPDATE SET sequence=EXCLUDED.sequence,started_at=EXCLUDED.started_at,cutoff=EXCLUDED.cutoff,payload=EXCLUDED.payload,home_payload=EXCLUDED.home_payload,published_security_version=EXCLUDED.published_security_version;
    UPDATE identity_sessions.subjects SET active=COALESCE(valid,false),security_version=v,provider_checked_at=start_at,sign_in_valid_from_at=cutoff_at WHERE tenant_id=t AND object_id=o;
    INSERT INTO identity_authority.mutations VALUES(p_operation,t,o,p_digest,e.revision,v,p);
    RETURN QUERY SELECT e.revision,v;
END $$;
CREATE FUNCTION identity_authority.check_commit() RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
DECLARE now_at timestamptz; p jsonb; request jsonb; receipt jsonb; linked_events uuid[]; home jsonb;
BEGIN
    now_at=clock_timestamp(); p=NEW.command_payload;
    SELECT request_canonical::jsonb,receipt_canonical::jsonb,event_ids INTO request,receipt,linked_events FROM security_audit.operation_receipts WHERE operation_id=NEW.operation_id;
    IF request IS NULL OR request->'target' IS DISTINCT FROM jsonb_build_object('tenantId',NEW.tenant_id::text,'objectId',NEW.object_id::text)
        OR request->>'commandSha256' IS DISTINCT FROM NEW.command_sha256::text OR (receipt->>'securityVersion')::bigint IS DISTINCT FROM NEW.security_version
        OR (receipt->>'resultingRevision')::bigint IS DISTINCT FROM NEW.revision THEN RAISE EXCEPTION 'authority receipt binding denied'; END IF;
    IF request->>'operationKind' IS DISTINCT FROM (CASE WHEN p->'decision'->>'operation'='RevokeSubject' THEN 'SubjectRevoked' ELSE 'AuthorityChanged' END)
        OR p->>'schemaVersion'='authority-command-v1' AND (request->'actor' IS DISTINCT FROM p->'decision'->'administrator'
            OR request->>'actorKind' IS DISTINCT FROM 'Human'
            OR request->'customerId' IS DISTINCT FROM COALESCE(p->'decision'->'scope'->'customerId','null'::jsonb)
            OR request->'projectId' IS DISTINCT FROM COALESCE(p->'decision'->'scope'->'projectId','null'::jsonb)
            OR request->'environmentId' IS DISTINCT FROM COALESCE(p->'decision'->'scope'->'environmentId','null'::jsonb)
            OR request->'assessmentId' IS DISTINCT FROM COALESCE(p->'decision'->'scope'->'assessmentId','null'::jsonb))
        OR p->>'schemaVersion'='provider-observation-v1' AND (request->>'actorKind' IS DISTINCT FROM 'Workload' OR request->'actor' IS DISTINCT FROM request->'issuer') THEN RAISE EXCEPTION 'authority actor/scope receipt denied'; END IF;
    IF NOT EXISTS(SELECT 1 FROM security_audit.events e WHERE e.operation_id=NEW.operation_id AND e.event_id=ANY(linked_events)
        AND e.canonical_event::jsonb->>'action' IN ('AuthorityChanged','SubjectRevoked')
        AND e.canonical_event::jsonb->>'securityVersion'=NEW.security_version::text
        AND e.canonical_event::jsonb->'target'=request->'target') THEN RAISE EXCEPTION 'authority matching event absent'; END IF;
    IF p->>'schemaVersion'='provider-observation-v1' AND (now_at-(p->>'startedAtUtc')::timestamptz>=interval '15 minutes'
        OR (p->>'completedAtUtc')::timestamptz>now_at OR (p->>'resourceCutoffUtc')::timestamptz>now_at) THEN RAISE EXCEPTION 'provider expired before commit'; END IF;
    IF p->>'schemaVersion'='provider-observation-v1' THEN
        SELECT home_payload INTO home FROM identity_authority.provider_observations WHERE tenant_id=NEW.tenant_id AND object_id=NEW.object_id;
        IF home IS NOT NULL AND (now_at-(home->>'checkedAtUtc')::timestamptz>=interval '15 minutes'
            OR (home->>'checkedAtUtc')::timestamptz>now_at OR (home->>'cutoffUtc')::timestamptz>now_at) THEN RAISE EXCEPTION 'home status expired before commit'; END IF;
    END IF;
    IF p->'guest' IS NOT NULL AND p->'guest'<>'null'::jsonb AND ((p->'guest'->>'expiresAtUtc')::timestamptz<=now_at
        OR now_at-(p->'guest'->>'lastReviewedAtUtc')::timestamptz>interval '30 days') THEN RAISE EXCEPTION 'external lifecycle expired before commit'; END IF;
    RETURN NEW;
END $$;
CREATE CONSTRAINT TRIGGER authority_commit_deadline AFTER INSERT ON identity_authority.mutations DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION identity_authority.check_commit();
REVOKE ALL ON ALL FUNCTIONS IN SCHEMA identity_authority FROM PUBLIC;
