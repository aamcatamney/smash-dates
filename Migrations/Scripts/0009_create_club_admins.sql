CREATE TABLE club_admins (
    club_id     uuid NOT NULL REFERENCES clubs(id) ON DELETE CASCADE,
    user_id     uuid NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    granted_at  timestamptz NOT NULL DEFAULT now(),
    granted_by  uuid NULL REFERENCES users(id) ON DELETE SET NULL,
    PRIMARY KEY (club_id, user_id)
);

CREATE INDEX ix_club_admins_user ON club_admins (user_id);
