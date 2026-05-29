CREATE TABLE seasons (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    league_id   uuid NOT NULL REFERENCES leagues(id) ON DELETE CASCADE,
    name        text NOT NULL,
    start_date  date NOT NULL,
    end_date    date NOT NULL,
    status      text NOT NULL DEFAULT 'Draft'
                CHECK (status IN ('Draft', 'Scheduling', 'Proposed', 'Active', 'Closed')),
    created_at  timestamptz NOT NULL DEFAULT now(),
    updated_at  timestamptz NOT NULL DEFAULT now(),
    CHECK (start_date <= end_date)
);

CREATE UNIQUE INDEX ux_seasons_league_name_lower ON seasons (league_id, lower(name));
