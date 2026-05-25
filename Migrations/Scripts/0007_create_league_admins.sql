CREATE TABLE league_admins (
    league_id   uuid NOT NULL REFERENCES leagues(id) ON DELETE CASCADE,
    user_id     uuid NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    granted_at  timestamptz NOT NULL DEFAULT now(),
    granted_by  uuid NULL REFERENCES users(id) ON DELETE SET NULL,
    PRIMARY KEY (league_id, user_id)
);

CREATE INDEX ix_league_admins_user ON league_admins (user_id);
