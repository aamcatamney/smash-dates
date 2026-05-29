CREATE TABLE blocked_dates (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    club_id     uuid NOT NULL REFERENCES clubs(id) ON DELETE CASCADE,
    scope       text NOT NULL CHECK (scope IN ('Club', 'Venue', 'Team')),
    venue_id    uuid NULL REFERENCES venues(id) ON DELETE CASCADE,
    team_id     uuid NULL REFERENCES teams(id) ON DELETE CASCADE,
    start_date  date NOT NULL,
    end_date    date NOT NULL,
    reason      text NOT NULL,
    created_at  timestamptz NOT NULL DEFAULT now(),
    CHECK (start_date <= end_date),
    -- Exactly the FK matching the scope is set; the other is null.
    CHECK (
        (scope = 'Club'  AND venue_id IS NULL     AND team_id IS NULL) OR
        (scope = 'Venue' AND venue_id IS NOT NULL AND team_id IS NULL) OR
        (scope = 'Team'  AND team_id  IS NOT NULL AND venue_id IS NULL)
    )
);

CREATE INDEX ix_blocked_dates_club ON blocked_dates (club_id);
CREATE INDEX ix_blocked_dates_venue ON blocked_dates (venue_id);
CREATE INDEX ix_blocked_dates_team ON blocked_dates (team_id);
