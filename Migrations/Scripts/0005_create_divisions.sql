CREATE TABLE divisions (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    league_id           uuid NOT NULL REFERENCES leagues(id) ON DELETE CASCADE,
    name                text NOT NULL,
    gender              text NOT NULL CHECK (gender IN ('Mens', 'Ladies', 'Mixed')),
    rank                integer NOT NULL,
    rubbers_per_match   integer NOT NULL CHECK (rubbers_per_match > 0),
    win_points          integer NOT NULL DEFAULT 2 CHECK (win_points >= 0),
    draw_points         integer NOT NULL DEFAULT 1 CHECK (draw_points >= 0),
    loss_points         integer NOT NULL DEFAULT 0 CHECK (loss_points >= 0),
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX ux_divisions_league_name_lower ON divisions (league_id, lower(name));
CREATE UNIQUE INDEX ux_divisions_league_gender_rank ON divisions (league_id, gender, rank);
