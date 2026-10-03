CREATE TRIGGER ai_lock_no_truncate BEFORE TRUNCATE ON synthetic_ai_execution.run_lock FOR EACH STATEMENT EXECUTE FUNCTION synthetic_ai_execution.reject_history_mutation();
CREATE TRIGGER ai_event_no_truncate BEFORE TRUNCATE ON synthetic_ai_execution.events FOR EACH STATEMENT EXECUTE FUNCTION synthetic_ai_execution.reject_history_mutation();
CREATE TRIGGER ai_proof_no_truncate BEFORE TRUNCATE ON synthetic_ai_execution.event_proof FOR EACH STATEMENT EXECUTE FUNCTION synthetic_ai_execution.reject_history_mutation();
CREATE TRIGGER ai_snapshot_no_truncate BEFORE TRUNCATE ON synthetic_ai_execution.accepted_snapshot FOR EACH STATEMENT EXECUTE FUNCTION synthetic_ai_execution.reject_history_mutation();
CREATE TRIGGER ai_scope_no_truncate BEFORE TRUNCATE ON synthetic_ai_execution.scope FOR EACH STATEMENT EXECUTE FUNCTION synthetic_ai_execution.reject_history_mutation();
