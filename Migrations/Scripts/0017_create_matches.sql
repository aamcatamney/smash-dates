CREATE TABLE matches (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    season_id     uuid NOT NULL REFERENCES seasons(id) ON DELETE CASCADE,
    division_id   uuid NOT NULL REFERENCES divisions(id) ON DELETE CASCADE,
    -- RESTRICT: a Team/Venue referenced by a Match cannot be hard-deleted (endpoints 409).
    home_team_id  uuid NOT NULL REFERENCES teams(id) ON DELETE RESTRICT,
    away_team_id  uuid NOT NULL REFERENCES teams(id) ON DELETE RESTRICT,
    venue_id      uuid NOT NULL REFERENCES venues(id) ON DELETE RESTRICT,
    match_date    date NOT NULL,
    status        text NOT NULL DEFAULT 'Proposed'
                  CHECK (status IN ('Proposed', 'Confirmed', 'Played', 'Postponed', 'Rejected')),
    created_at    timestamptz NOT NULL DEFAULT now(),
    CHECK (home_team_id <> away_team_id)
);

CREATE INDEX ix_matches_season ON matches (season_id);
CREATE INDEX ix_matches_division ON matches (division_id);
CREATE INDEX ix_matches_venue ON matches (venue_id);
