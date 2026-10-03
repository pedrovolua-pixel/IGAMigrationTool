CREATE TABLE synthetic_ai_execution.event_proof (
    event_id uuid PRIMARY KEY REFERENCES synthetic_ai_execution.events(event_id), digest text NOT NULL);
CREATE TABLE synthetic_ai_execution.accepted_snapshot (
    attempt_id uuid PRIMARY KEY, canonical text NOT NULL, digest text NOT NULL, source_json text NOT NULL);
CREATE TRIGGER ai_event_proof_immutable BEFORE UPDATE OR DELETE ON synthetic_ai_execution.event_proof FOR EACH ROW EXECUTE FUNCTION synthetic_ai_execution.reject_history_mutation();
CREATE TRIGGER ai_snapshot_immutable BEFORE UPDATE OR DELETE ON synthetic_ai_execution.accepted_snapshot FOR EACH ROW EXECUTE FUNCTION synthetic_ai_execution.reject_history_mutation();
