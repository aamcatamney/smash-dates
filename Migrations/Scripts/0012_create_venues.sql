CREATE TABLE venues (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    club_id     uuid NOT NULL REFERENCES clubs(id) ON DELETE CASCADE,
    name        text NOT NULL,
    capacity    integer NOT NULL DEFAULT 1 CHECK (capacity IN (1, 2)),
    created_at  timestamptz NOT NULL DEFAULT now(),
    updated_at  timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX ux_venues_club_name_lower ON venues (club_id, lower(name));
