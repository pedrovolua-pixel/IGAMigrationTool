CREATE TABLE synthetic_ai_execution.scope (
    singleton boolean PRIMARY KEY CHECK(singleton), customer_id text NOT NULL, project_id text NOT NULL, environment_id text NOT NULL);
CREATE TABLE synthetic_ai_execution.run_lock (
    run_id uuid PRIMARY KEY, canonical text NOT NULL, digest text NOT NULL);
CREATE TABLE synthetic_ai_execution.current_state (
    run_id uuid PRIMARY KEY REFERENCES synthetic_ai_execution.run_lock(run_id), revision bigint NOT NULL CHECK(revision>0), canonical text NOT NULL, digest text NOT NULL);
CREATE TABLE synthetic_ai_execution.events (
    run_id uuid NOT NULL REFERENCES synthetic_ai_execution.run_lock(run_id), revision bigint NOT NULL CHECK(revision>0),
    event_id uuid NOT NULL, actor_id text NOT NULL, kind text NOT NULL, command_digest text NOT NULL,
    before_digest text NOT NULL, after_digest text NOT NULL, after_canonical text NOT NULL, receipt_canonical text NOT NULL,
    recorded_at timestamptz NOT NULL, PRIMARY KEY(run_id,revision), UNIQUE(event_id));
CREATE TABLE synthetic_ai_execution.counter (
    counter_key text PRIMARY KEY, charged integer NOT NULL CHECK(charged>=0), held integer NOT NULL CHECK(held>=0), digest text NOT NULL);
CREATE FUNCTION synthetic_ai_execution.reject_history_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'AI immutable history denied'; END; $$;
CREATE TRIGGER ai_lock_immutable BEFORE UPDATE OR DELETE ON synthetic_ai_execution.run_lock FOR EACH ROW EXECUTE FUNCTION synthetic_ai_execution.reject_history_mutation();
CREATE TRIGGER ai_event_immutable BEFORE UPDATE OR DELETE ON synthetic_ai_execution.events FOR EACH ROW EXECUTE FUNCTION synthetic_ai_execution.reject_history_mutation();
