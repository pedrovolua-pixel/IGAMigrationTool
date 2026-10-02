-- Control-plane schema only. Execute with the approved migration identity.
-- No customer database, password, data grant or startup auto-migration.
CREATE SCHEMA IF NOT EXISTS identity_sessions;
CREATE TABLE identity_sessions.subjects (
    tenant_id uuid NOT NULL,
    object_id uuid NOT NULL,
    active boolean NOT NULL,
    security_version bigint NOT NULL CHECK (security_version > 0),
    provider_checked_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, object_id)
);
CREATE TABLE identity_sessions.tickets (
    key_hash char(64) PRIMARY KEY,
    session_reference uuid NOT NULL UNIQUE,
    tenant_id uuid NOT NULL,
    object_id uuid NOT NULL,
    security_version bigint NOT NULL CHECK (security_version > 0),
    created_at timestamptz NOT NULL,
    last_seen_at timestamptz NOT NULL,
    absolute_expires_at timestamptz NOT NULL,
    revoked_at timestamptz,
    protected_ticket bytea NOT NULL,
    FOREIGN KEY (tenant_id, object_id) REFERENCES identity_sessions.subjects (tenant_id, object_id),
    CHECK (absolute_expires_at > created_at AND absolute_expires_at <= created_at + interval '8 hours'),
    CHECK (last_seen_at >= created_at AND last_seen_at < absolute_expires_at)
);
CREATE INDEX tickets_subject ON identity_sessions.tickets (tenant_id, object_id);
