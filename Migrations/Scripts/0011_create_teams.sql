CREATE TABLE teams (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    club_id     uuid NOT NULL REFERENCES clubs(id) ON DELETE CASCADE,
    name        text NOT NULL,
    gender      text NOT NULL CHECK (gender IN ('Mens', 'Ladies', 'Mixed')),
    created_at  timestamptz NOT NULL DEFAULT now(),
    updated_at  timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX ux_teams_club_name_lower ON teams (club_id, lower(name));
