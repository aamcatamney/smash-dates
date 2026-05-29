CREATE TABLE season_entries (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    season_id   uuid NOT NULL REFERENCES seasons(id) ON DELETE CASCADE,
    division_id uuid NOT NULL REFERENCES divisions(id) ON DELETE CASCADE,
    -- RESTRICT so a Team that is entered in a Season cannot be hard-deleted; the
    -- Team delete endpoint pre-checks this and returns 409 (see CONTEXT.md guarded delete).
    team_id     uuid NOT NULL REFERENCES teams(id) ON DELETE RESTRICT,
    created_at  timestamptz NOT NULL DEFAULT now(),
    -- A Team plays in at most one Division per Season.
    UNIQUE (season_id, team_id)
);

CREATE INDEX ix_season_entries_season ON season_entries (season_id);
CREATE INDEX ix_season_entries_team ON season_entries (team_id);
