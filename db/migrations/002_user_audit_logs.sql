-- migrate:up
CREATE TABLE IF NOT EXISTS user_audit_logs (
    id          BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id     TEXT NOT NULL,
    event_type  TEXT NOT NULL,
    source      TEXT NOT NULL,
    payload     JSONB NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_user_audit_logs_user_id ON user_audit_logs (user_id);
CREATE INDEX idx_user_audit_logs_created_at ON user_audit_logs (created_at);

-- migrate:down
DROP TABLE IF EXISTS user_audit_logs;
