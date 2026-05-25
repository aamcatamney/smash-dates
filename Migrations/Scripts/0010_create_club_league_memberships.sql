CREATE TABLE club_league_memberships (
    id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    club_id        uuid NOT NULL REFERENCES clubs(id) ON DELETE CASCADE,
    league_id      uuid NOT NULL REFERENCES leagues(id) ON DELETE CASCADE,
    status         text NOT NULL CHECK (status IN ('Pending', 'Accepted', 'Declined', 'Withdrawn', 'Expelled')),
    invited_at     timestamptz NOT NULL DEFAULT now(),
    invited_by     uuid NULL REFERENCES users(id) ON DELETE SET NULL,
    responded_at   timestamptz NULL,
    responded_by   uuid NULL REFERENCES users(id) ON DELETE SET NULL
);

-- Only one non-terminal membership row per (club, league). Terminal statuses
-- (Declined, Withdrawn, Expelled) may be repeated as a re-invite history.
CREATE UNIQUE INDEX ux_club_league_memberships_active
    ON club_league_memberships (club_id, league_id)
    WHERE status IN ('Pending', 'Accepted');

CREATE INDEX ix_club_league_memberships_league ON club_league_memberships (league_id);
CREATE INDEX ix_club_league_memberships_club ON club_league_memberships (club_id);
