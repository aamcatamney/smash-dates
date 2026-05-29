CREATE TABLE season_weeks (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    season_id   uuid NOT NULL REFERENCES seasons(id) ON DELETE CASCADE,
    start_date  date NOT NULL,
    end_date    date NOT NULL,
    week_type   text NOT NULL CHECK (week_type IN ('Level', 'Mixed')),
    created_at  timestamptz NOT NULL DEFAULT now(),
    CHECK (start_date <= end_date)
);

-- Order is derived from start_date; non-overlap is enforced in application code
-- (see docs/adr/0002). This index also rejects two Weeks sharing a start date.
CREATE UNIQUE INDEX ux_season_weeks_season_start ON season_weeks (season_id, start_date);
