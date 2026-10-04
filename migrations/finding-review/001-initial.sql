CREATE TABLE synthetic_review.data_plane_scope (
    singleton boolean PRIMARY KEY CHECK (singleton),
    customer_id text NOT NULL, project_id text NOT NULL, environment_id text NOT NULL,
    UNIQUE (customer_id, project_id, environment_id)
);
CREATE TABLE synthetic_review.run_seeds (
    run_id uuid PRIMARY KEY,
    customer_id text NOT NULL, project_id text NOT NULL, environment_id text NOT NULL,
    seed_json text NOT NULL, seed_digest text NOT NULL CHECK (seed_digest ~ '^[0-9a-f]{64}$'),
    created_at timestamptz NOT NULL,
    FOREIGN KEY (customer_id, project_id, environment_id) REFERENCES synthetic_review.data_plane_scope (customer_id, project_id, environment_id)
);
CREATE TABLE synthetic_review.finding_seeds (
    run_id uuid NOT NULL REFERENCES synthetic_review.run_seeds (run_id),
    finding_id text NOT NULL CHECK (finding_id ~ '^[0-9a-f]{64}$'),
    seed_json text NOT NULL, seed_digest text NOT NULL CHECK (seed_digest ~ '^[0-9a-f]{64}$'),
    PRIMARY KEY (run_id, finding_id)
);
CREATE TABLE synthetic_review.finding_current (
    run_id uuid NOT NULL, finding_id text NOT NULL,
    revision bigint NOT NULL CHECK (revision >= 0), current_json text NOT NULL,
    PRIMARY KEY (run_id, finding_id),
    FOREIGN KEY (run_id, finding_id) REFERENCES synthetic_review.finding_seeds (run_id, finding_id)
);
CREATE TABLE synthetic_review.events (
    run_id uuid NOT NULL, finding_id text NOT NULL, event_id uuid NOT NULL,
    expected_revision bigint NOT NULL CHECK (expected_revision >= 0),
    result_revision bigint NOT NULL CHECK (result_revision = expected_revision + 1),
    payload_digest text NOT NULL CHECK (payload_digest ~ '^[0-9a-f]{64}$'),
    event_json text NOT NULL, event_digest text NOT NULL CHECK (event_digest ~ '^[0-9a-f]{64}$'),
    after_json text NOT NULL, recorded_at timestamptz NOT NULL,
    PRIMARY KEY (run_id, finding_id, event_id),
    UNIQUE (run_id, finding_id, result_revision),
    FOREIGN KEY (run_id, finding_id) REFERENCES synthetic_review.finding_seeds (run_id, finding_id)
);
CREATE FUNCTION synthetic_review.refuse_rewrite() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'synthetic review immutable record cannot be rewritten' USING ERRCODE = '55000';
END;
$$;
CREATE TRIGGER scope_immutable BEFORE UPDATE OR DELETE OR TRUNCATE ON synthetic_review.data_plane_scope FOR EACH STATEMENT EXECUTE FUNCTION synthetic_review.refuse_rewrite();
CREATE TRIGGER run_seed_immutable BEFORE UPDATE OR DELETE OR TRUNCATE ON synthetic_review.run_seeds FOR EACH STATEMENT EXECUTE FUNCTION synthetic_review.refuse_rewrite();
CREATE TRIGGER finding_seed_immutable BEFORE UPDATE OR DELETE OR TRUNCATE ON synthetic_review.finding_seeds FOR EACH STATEMENT EXECUTE FUNCTION synthetic_review.refuse_rewrite();
CREATE TRIGGER event_immutable BEFORE UPDATE OR DELETE OR TRUNCATE ON synthetic_review.events FOR EACH STATEMENT EXECUTE FUNCTION synthetic_review.refuse_rewrite();
CREATE FUNCTION synthetic_review.guard_current() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'synthetic review current cannot be deleted' USING ERRCODE = '55000';
    END IF;
    IF NEW.run_id <> OLD.run_id OR NEW.finding_id <> OLD.finding_id OR NEW.revision <> OLD.revision + 1
        OR NOT EXISTS (SELECT 1 FROM synthetic_review.events e WHERE e.run_id=NEW.run_id AND e.finding_id=NEW.finding_id AND e.result_revision=NEW.revision AND e.after_json=NEW.current_json) THEN
        RAISE EXCEPTION 'synthetic review current must match one appended revision' USING ERRCODE = '55000';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER current_revision_guard BEFORE UPDATE OR DELETE ON synthetic_review.finding_current FOR EACH ROW EXECUTE FUNCTION synthetic_review.guard_current();
CREATE TRIGGER current_truncate_guard BEFORE TRUNCATE ON synthetic_review.finding_current FOR EACH STATEMENT EXECUTE FUNCTION synthetic_review.refuse_rewrite();
CREATE TRIGGER migration_immutable BEFORE UPDATE OR DELETE OR TRUNCATE ON synthetic_review.schema_migrations FOR EACH STATEMENT EXECUTE FUNCTION synthetic_review.refuse_rewrite();
