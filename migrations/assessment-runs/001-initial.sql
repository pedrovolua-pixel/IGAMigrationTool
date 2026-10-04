-- SYNTHETIC local customer-plane expansion. No destructive rollback or customer installation.
CREATE TABLE synthetic_assessment.data_plane_scope (
    singleton boolean PRIMARY KEY CHECK (singleton), customer_id text NOT NULL,
    project_id text NOT NULL, environment_id text NOT NULL
);
CREATE TABLE synthetic_assessment.runs (
    run_id uuid PRIMARY KEY,
    customer_id text NOT NULL, project_id text NOT NULL, environment_id text NOT NULL,
    idempotency_key text NOT NULL, baseline_catalog_id text NOT NULL, profile_catalog_id text NOT NULL,
    input_digest text NOT NULL, versions_json text NOT NULL, plan_json text NOT NULL,
    state integer NOT NULL CHECK (state BETWEEN 0 AND 4), revision bigint NOT NULL CHECK (revision > 0),
    cancel_requested boolean NOT NULL, checkpoint_sequence bigint NOT NULL CHECK (checkpoint_sequence >= 0),
    lease_owner text NULL, lease_generation uuid NULL, lease_expires_at timestamp with time zone NULL,
    summary_json text NULL, active_work_json text NOT NULL, created_at timestamp with time zone NOT NULL, updated_at timestamp with time zone NOT NULL,
    CONSTRAINT uq_run_start UNIQUE (project_id, idempotency_key),
    CONSTRAINT ck_lease_tuple CHECK ((lease_owner IS NULL AND lease_generation IS NULL AND lease_expires_at IS NULL)
        OR (lease_owner IS NOT NULL AND lease_generation IS NOT NULL AND lease_expires_at IS NOT NULL))
);
CREATE TABLE synthetic_assessment.plan_units (
    run_id uuid NOT NULL REFERENCES synthetic_assessment.runs(run_id), inventory_id text NOT NULL, category_id text NOT NULL,
    PRIMARY KEY (run_id, inventory_id, category_id)
);
CREATE TABLE synthetic_assessment.results (
    run_id uuid NOT NULL, inventory_id text NOT NULL, category_id text NOT NULL,
    state integer NOT NULL CHECK (state BETWEEN 0 AND 9), reason_code text NULL, responsible_stage text NULL,
    evidence_reference text NULL, result_digest text NOT NULL,
    PRIMARY KEY (run_id, inventory_id, category_id),
    FOREIGN KEY (run_id, inventory_id, category_id) REFERENCES synthetic_assessment.plan_units(run_id, inventory_id, category_id)
);
CREATE TABLE synthetic_assessment.attempts (
    run_id uuid NOT NULL, inventory_id text NOT NULL, category_id text NOT NULL,
    attempt_number integer NOT NULL CHECK (attempt_number > 0), outcome integer NOT NULL CHECK (outcome BETWEEN 0 AND 2),
    reason_code text NULL, lease_generation uuid NOT NULL, created_at timestamp with time zone NOT NULL,
    PRIMARY KEY (run_id, inventory_id, category_id, attempt_number),
    FOREIGN KEY (run_id, inventory_id, category_id) REFERENCES synthetic_assessment.plan_units(run_id, inventory_id, category_id)
);
CREATE TABLE synthetic_assessment.outbox (
    event_id uuid PRIMARY KEY, run_id uuid NOT NULL REFERENCES synthetic_assessment.runs(run_id), run_revision bigint NOT NULL,
    schema_version text NOT NULL, minimum_worker_version text NOT NULL, kind text NOT NULL,
    created_at timestamp with time zone NOT NULL, dispatched_at timestamp with time zone NULL,
    CONSTRAINT uq_outbox_transition UNIQUE (run_id, run_revision, kind)
);
CREATE TABLE synthetic_assessment.inbox (
    consumer_id text NOT NULL, event_id uuid NOT NULL REFERENCES synthetic_assessment.outbox(event_id),
    received_at timestamp with time zone NOT NULL,
    PRIMARY KEY (consumer_id, event_id)
);
CREATE INDEX ix_runs_scope_created ON synthetic_assessment.runs(customer_id, project_id, environment_id, created_at);
CREATE INDEX ix_outbox_pending ON synthetic_assessment.outbox(created_at) WHERE dispatched_at IS NULL;

CREATE FUNCTION synthetic_assessment.refuse_immutable_change() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'Immutable synthetic run evidence cannot be updated or deleted'; END;
$$;
CREATE TRIGGER immutable_plan BEFORE UPDATE OR DELETE ON synthetic_assessment.plan_units
    FOR EACH ROW EXECUTE FUNCTION synthetic_assessment.refuse_immutable_change();
CREATE TRIGGER immutable_results BEFORE UPDATE OR DELETE ON synthetic_assessment.results
    FOR EACH ROW EXECUTE FUNCTION synthetic_assessment.refuse_immutable_change();
CREATE TRIGGER immutable_attempts BEFORE UPDATE OR DELETE ON synthetic_assessment.attempts
    FOR EACH ROW EXECUTE FUNCTION synthetic_assessment.refuse_immutable_change();
CREATE TRIGGER immutable_data_plane_scope BEFORE UPDATE OR DELETE ON synthetic_assessment.data_plane_scope
    FOR EACH ROW EXECUTE FUNCTION synthetic_assessment.refuse_immutable_change();
CREATE FUNCTION synthetic_assessment.protect_run_inputs() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF (NEW.customer_id, NEW.project_id, NEW.environment_id, NEW.idempotency_key, NEW.baseline_catalog_id,
        NEW.profile_catalog_id, NEW.input_digest, NEW.versions_json, NEW.plan_json, NEW.created_at)
        IS DISTINCT FROM
       (OLD.customer_id, OLD.project_id, OLD.environment_id, OLD.idempotency_key, OLD.baseline_catalog_id,
        OLD.profile_catalog_id, OLD.input_digest, OLD.versions_json, OLD.plan_json, OLD.created_at)
    THEN RAISE EXCEPTION 'Synthetic run input lock is immutable'; END IF;
    IF OLD.summary_json IS NOT NULL AND NEW.summary_json IS DISTINCT FROM OLD.summary_json
    THEN RAISE EXCEPTION 'Synthetic coverage summary is immutable'; END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER immutable_run_inputs BEFORE UPDATE ON synthetic_assessment.runs
    FOR EACH ROW EXECUTE FUNCTION synthetic_assessment.protect_run_inputs();
