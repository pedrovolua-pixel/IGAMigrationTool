-- Additive control-plane migration; explicit migration identity only.
-- Existing rows have no cutoff evidence and deny until trusted administration
-- refreshes it. No startup migration, provider enrollment or data grant.
ALTER TABLE identity_sessions.subjects ADD COLUMN sign_in_valid_from_at timestamptz;
CREATE TABLE identity_sessions.pending_challenges (
    key_hash char(64) PRIMARY KEY,
    issued_at timestamptz NOT NULL,
    consumed_at timestamptz,
    CHECK (consumed_at IS NULL OR (consumed_at >= issued_at AND consumed_at < issued_at + interval '15 minutes'))
);
-- No automatic cleanup/retention policy is selected by this migration.
