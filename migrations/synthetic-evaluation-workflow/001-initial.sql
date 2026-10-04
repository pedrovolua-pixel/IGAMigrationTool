CREATE TABLE synthetic_evaluation_workflow.workspace (
    singleton boolean PRIMARY KEY CHECK (singleton),
    source_json text NOT NULL, source_digest text NOT NULL, initial_registry_digest text NOT NULL,
    aggregate_revision bigint NOT NULL CHECK (aggregate_revision BETWEEN 0 AND 1000),
    last_event_sequence bigint NOT NULL CHECK (last_event_sequence BETWEEN 0 AND 1000),
    current_registry_version_id text NOT NULL
);
CREATE TABLE synthetic_evaluation_workflow.registries (
    version_id text PRIMARY KEY, revision bigint UNIQUE NOT NULL CHECK (revision BETWEEN 1 AND 1001),
    canonical_json text NOT NULL, digest text NOT NULL, recorded_at timestamptz NOT NULL
);
CREATE TABLE synthetic_evaluation_workflow.members (
    member_id text PRIMARY KEY, revision bigint NOT NULL CHECK (revision BETWEEN 0 AND 1000),
    current_json text NOT NULL
);
CREATE TABLE synthetic_evaluation_workflow.events (
    event_id uuid PRIMARY KEY, sequence bigint UNIQUE NOT NULL CHECK (sequence BETWEEN 1 AND 1000),
    aggregate_revision bigint UNIQUE NOT NULL CHECK (aggregate_revision BETWEEN 1 AND 1000),
    member_id text NOT NULL REFERENCES synthetic_evaluation_workflow.members(member_id),
    member_revision bigint NOT NULL, actor_id text NOT NULL, command_json text NOT NULL,
    command_digest text NOT NULL, event_json text NOT NULL, event_digest text NOT NULL, receipt_json text NOT NULL
);
CREATE TABLE synthetic_evaluation_workflow.versions (
    version bigint PRIMARY KEY CHECK (version BETWEEN 0 AND 1000),
    registry_version_id text NOT NULL REFERENCES synthetic_evaluation_workflow.registries(version_id),
    last_event_sequence bigint NOT NULL, manifest_json text NOT NULL, manifest_digest text NOT NULL,
    accuracy_json text NOT NULL, accuracy_digest text NOT NULL, warning_json text NOT NULL,
    warning_digest text NOT NULL, snapshot_digest text UNIQUE NOT NULL, recorded_at timestamptz NOT NULL
);
CREATE FUNCTION synthetic_evaluation_workflow.immutable_guard() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'Immutable evaluation metadata cannot be overwritten';
END;
$$;
CREATE TRIGGER registries_immutable BEFORE UPDATE OR DELETE ON synthetic_evaluation_workflow.registries
    FOR EACH ROW EXECUTE FUNCTION synthetic_evaluation_workflow.immutable_guard();
CREATE TRIGGER events_immutable BEFORE UPDATE OR DELETE ON synthetic_evaluation_workflow.events
    FOR EACH ROW EXECUTE FUNCTION synthetic_evaluation_workflow.immutable_guard();
CREATE TRIGGER versions_immutable BEFORE UPDATE OR DELETE ON synthetic_evaluation_workflow.versions
    FOR EACH ROW EXECUTE FUNCTION synthetic_evaluation_workflow.immutable_guard();
CREATE FUNCTION synthetic_evaluation_workflow.source_guard() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'Frozen source cannot be removed'; END IF;
    IF NEW.source_json IS DISTINCT FROM OLD.source_json OR NEW.source_digest IS DISTINCT FROM OLD.source_digest
        OR NEW.initial_registry_digest IS DISTINCT FROM OLD.initial_registry_digest THEN
        RAISE EXCEPTION 'Frozen source cannot be overwritten';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER source_immutable BEFORE UPDATE OR DELETE ON synthetic_evaluation_workflow.workspace
    FOR EACH ROW EXECUTE FUNCTION synthetic_evaluation_workflow.source_guard();
