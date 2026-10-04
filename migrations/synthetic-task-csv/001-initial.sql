CREATE TABLE synthetic_task_csv.data_plane_scope (
 singleton boolean PRIMARY KEY CHECK (singleton), customer_id text NOT NULL, project_id text NOT NULL, environment_id text NOT NULL
);
CREATE TABLE synthetic_task_csv.export_audit (
 event_id uuid PRIMARY KEY, request_id uuid NOT NULL, ordinal integer NOT NULL CHECK(ordinal BETWEEN 0 AND 4), run_id uuid NOT NULL, actor_id text NOT NULL,
 customer_id text NOT NULL, project_id text NOT NULL, environment_id text NOT NULL,
 recorded_at timestamptz NOT NULL, stage text NOT NULL CHECK(stage IN ('Request','Dispatch','Render','Delivery','Denial')),
 outcome text NOT NULL CHECK(outcome IN ('Accepted','Denied')), reason text NOT NULL CHECK(reason IN ('None','InvalidInput','Denied','SourceConflict','IntegrityMismatch','RendererFailure','LimitExceeded','AuditUnavailable')),
 snapshot_digest text, output_sha256 text, row_count integer CHECK(row_count BETWEEN 0 AND 1000),
 UNIQUE(request_id,ordinal),
 contract_version text NOT NULL CHECK(contract_version='synthetic-task-csv-v1'), event_digest text NOT NULL CHECK(event_digest ~ '^[0-9a-f]{64}$'),
 CHECK(snapshot_digest IS NULL OR snapshot_digest ~ '^[0-9a-f]{64}$'), CHECK(output_sha256 IS NULL OR output_sha256 ~ '^[0-9a-f]{64}$'),
 CHECK((stage='Denial' AND outcome='Denied' AND reason<>'None') OR (stage<>'Denial' AND outcome='Accepted' AND reason='None'))
);
CREATE INDEX export_audit_request ON synthetic_task_csv.export_audit(request_id);
CREATE FUNCTION synthetic_task_csv.reject_change() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'CSV metadata audit is append only'; END $$;
CREATE TRIGGER export_audit_immutable BEFORE UPDATE OR DELETE OR TRUNCATE ON synthetic_task_csv.export_audit FOR EACH STATEMENT EXECUTE FUNCTION synthetic_task_csv.reject_change();
CREATE TRIGGER scope_immutable BEFORE UPDATE OR DELETE OR TRUNCATE ON synthetic_task_csv.data_plane_scope FOR EACH STATEMENT EXECUTE FUNCTION synthetic_task_csv.reject_change();
