-- Add only the bounded terminal marker after an authorized Delivery; migration001 stays immutable.
ALTER TABLE synthetic_task_csv.export_audit DROP CONSTRAINT export_audit_reason_check;
ALTER TABLE synthetic_task_csv.export_audit ADD CONSTRAINT export_audit_reason_check
 CHECK(reason IN ('None','InvalidInput','Denied','SourceConflict','IntegrityMismatch','RendererFailure','LimitExceeded','AuditUnavailable','TransferInterrupted'));
ALTER TABLE synthetic_task_csv.export_audit ADD CONSTRAINT export_audit_transfer_interrupted_check
 CHECK(reason<>'TransferInterrupted' OR (stage='Denial' AND ordinal=4 AND snapshot_digest IS NOT NULL AND output_sha256 IS NOT NULL AND row_count IS NOT NULL));
