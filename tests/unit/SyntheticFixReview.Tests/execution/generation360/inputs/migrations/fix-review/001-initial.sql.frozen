CREATE TABLE synthetic_fix_review.data_plane_scope (
    singleton boolean PRIMARY KEY CHECK (singleton),
    customer_id text NOT NULL, project_id text NOT NULL, environment_id text NOT NULL,
    UNIQUE (customer_id, project_id, environment_id)
);
CREATE TABLE synthetic_fix_review.source_versions (
    run_id uuid NOT NULL, source_digest text NOT NULL CHECK (source_digest ~ '^[0-9a-f]{64}$'),
    customer_id text NOT NULL, project_id text NOT NULL, environment_id text NOT NULL,
    canonical_package text NOT NULL, binding_json text NOT NULL, artifacts_json text NOT NULL,
    binding_digest text NOT NULL CHECK (binding_digest ~ '^[0-9a-f]{64}$'),
    created_at timestamptz NOT NULL,
    PRIMARY KEY (run_id, source_digest),
    FOREIGN KEY (customer_id, project_id, environment_id) REFERENCES synthetic_fix_review.data_plane_scope (customer_id, project_id, environment_id)
);
CREATE TABLE synthetic_fix_review.artifact_seeds (
    run_id uuid NOT NULL, artifact_id text NOT NULL CHECK (artifact_id ~ '^[0-9a-f]{64}$'),
    source_digest text NOT NULL, artifact_json text NOT NULL,
    artifact_digest text NOT NULL CHECK (artifact_digest ~ '^[0-9a-f]{64}$'),
    PRIMARY KEY (run_id, artifact_id),
    FOREIGN KEY (run_id, source_digest) REFERENCES synthetic_fix_review.source_versions (run_id, source_digest)
);
CREATE TABLE synthetic_fix_review.artifact_current (
    run_id uuid NOT NULL, artifact_id text NOT NULL,
    revision bigint NOT NULL CHECK (revision BETWEEN 0 AND 9007199254740991), current_json text NOT NULL,
    PRIMARY KEY (run_id, artifact_id),
    FOREIGN KEY (run_id, artifact_id) REFERENCES synthetic_fix_review.artifact_seeds (run_id, artifact_id)
);
CREATE TABLE synthetic_fix_review.events (
    run_id uuid NOT NULL, artifact_id text NOT NULL, event_id uuid NOT NULL,
    expected_revision bigint NOT NULL CHECK (expected_revision BETWEEN 0 AND 9007199254740990),
    result_revision bigint NOT NULL CHECK (result_revision = expected_revision + 1),
    source_digest text NOT NULL,
    command_digest text NOT NULL CHECK (command_digest ~ '^[0-9a-f]{64}$'),
    event_json text NOT NULL, event_digest text NOT NULL CHECK (event_digest ~ '^[0-9a-f]{64}$'),
    after_json text NOT NULL, recorded_at timestamptz NOT NULL,
    PRIMARY KEY (run_id, artifact_id, event_id), UNIQUE (run_id, artifact_id, result_revision),
    FOREIGN KEY (run_id, artifact_id) REFERENCES synthetic_fix_review.artifact_seeds (run_id, artifact_id),
    FOREIGN KEY (run_id, source_digest) REFERENCES synthetic_fix_review.source_versions (run_id, source_digest)
);
CREATE TABLE synthetic_fix_review.receipts (
    run_id uuid NOT NULL, artifact_id text NOT NULL, event_id uuid NOT NULL,
    receipt_json text NOT NULL, receipt_digest text NOT NULL CHECK (receipt_digest ~ '^[0-9a-f]{64}$'),
    PRIMARY KEY (run_id, artifact_id, event_id),
    FOREIGN KEY (run_id, artifact_id, event_id) REFERENCES synthetic_fix_review.events (run_id, artifact_id, event_id)
);
CREATE FUNCTION synthetic_fix_review.refuse_rewrite() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'synthetic artifact review immutable record cannot be rewritten' USING ERRCODE = '55000';
END;
$$;
CREATE TRIGGER scope_immutable BEFORE UPDATE OR DELETE OR TRUNCATE ON synthetic_fix_review.data_plane_scope FOR EACH STATEMENT EXECUTE FUNCTION synthetic_fix_review.refuse_rewrite();
CREATE TRIGGER source_immutable BEFORE UPDATE OR DELETE OR TRUNCATE ON synthetic_fix_review.source_versions FOR EACH STATEMENT EXECUTE FUNCTION synthetic_fix_review.refuse_rewrite();
CREATE TRIGGER artifact_seed_immutable BEFORE UPDATE OR DELETE OR TRUNCATE ON synthetic_fix_review.artifact_seeds FOR EACH STATEMENT EXECUTE FUNCTION synthetic_fix_review.refuse_rewrite();
CREATE TRIGGER event_immutable BEFORE UPDATE OR DELETE OR TRUNCATE ON synthetic_fix_review.events FOR EACH STATEMENT EXECUTE FUNCTION synthetic_fix_review.refuse_rewrite();
CREATE TRIGGER receipt_immutable BEFORE UPDATE OR DELETE OR TRUNCATE ON synthetic_fix_review.receipts FOR EACH STATEMENT EXECUTE FUNCTION synthetic_fix_review.refuse_rewrite();
CREATE TRIGGER migration_immutable BEFORE UPDATE OR DELETE OR TRUNCATE ON synthetic_fix_review.schema_migrations FOR EACH STATEMENT EXECUTE FUNCTION synthetic_fix_review.refuse_rewrite();
CREATE FUNCTION synthetic_fix_review.guard_current() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'synthetic artifact review current cannot be deleted' USING ERRCODE = '55000';
    END IF;
    IF TG_OP = 'INSERT' THEN
        IF NEW.revision <> 0 THEN
            RAISE EXCEPTION 'synthetic artifact review current starts at zero' USING ERRCODE = '55000';
        END IF;
    ELSIF NEW.run_id <> OLD.run_id OR NEW.artifact_id <> OLD.artifact_id OR NEW.revision <> OLD.revision + 1
        OR NOT EXISTS (SELECT 1 FROM synthetic_fix_review.events e WHERE e.run_id=NEW.run_id AND e.artifact_id=NEW.artifact_id AND e.result_revision=NEW.revision AND e.after_json=NEW.current_json) THEN
        RAISE EXCEPTION 'synthetic artifact review current must match one appended revision' USING ERRCODE = '55000';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER current_revision_guard BEFORE INSERT OR UPDATE OR DELETE ON synthetic_fix_review.artifact_current FOR EACH ROW EXECUTE FUNCTION synthetic_fix_review.guard_current();
CREATE TRIGGER current_truncate_guard BEFORE TRUNCATE ON synthetic_fix_review.artifact_current FOR EACH STATEMENT EXECUTE FUNCTION synthetic_fix_review.refuse_rewrite();
CREATE FUNCTION synthetic_fix_review.guard_event_commit() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM synthetic_fix_review.receipts r WHERE r.run_id=NEW.run_id AND r.artifact_id=NEW.artifact_id AND r.event_id=NEW.event_id)
       OR NOT EXISTS (SELECT 1 FROM synthetic_fix_review.artifact_current c WHERE c.run_id=NEW.run_id AND c.artifact_id=NEW.artifact_id AND c.revision>=NEW.result_revision) THEN
        RAISE EXCEPTION 'synthetic artifact review event requires atomic current and receipt' USING ERRCODE = '55000';
    END IF;
    RETURN NEW;
END;
$$;
CREATE CONSTRAINT TRIGGER event_commit_guard AFTER INSERT ON synthetic_fix_review.events DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION synthetic_fix_review.guard_event_commit();
