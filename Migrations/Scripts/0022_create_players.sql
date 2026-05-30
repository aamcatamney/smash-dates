-- Players: global, admin-managed person records (no login). See docs/adr/0003.
CREATE TABLE players (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    full_name   text NOT NULL,
    gender      text NOT NULL CHECK (gender IN ('Male', 'Female')),
    created_at  timestamptz NOT NULL DEFAULT now(),
    updated_at  timestamptz NOT NULL DEFAULT now()
);

-- Player <-> Club affiliation. Member = club may register them; Visitor = guest.
CREATE TABLE player_clubs (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    player_id   uuid NOT NULL REFERENCES players(id) ON DELETE CASCADE,
    club_id     uuid NOT NULL REFERENCES clubs(id) ON DELETE CASCADE,
    type        text NOT NULL CHECK (type IN ('Member', 'Visitor')),
    created_at  timestamptz NOT NULL DEFAULT now(),
    updated_at  timestamptz NOT NULL DEFAULT now(),
    UNIQUE (player_id, club_id)
);
CREATE INDEX ix_player_clubs_club ON player_clubs (club_id);
CREATE INDEX ix_player_clubs_player ON player_clubs (player_id);

-- Discipline registration scoped to (player, club, league, discipline).
CREATE TABLE discipline_registrations (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    player_id     uuid NOT NULL REFERENCES players(id) ON DELETE CASCADE,
    club_id       uuid NOT NULL REFERENCES clubs(id) ON DELETE CASCADE,
    league_id     uuid NOT NULL REFERENCES leagues(id) ON DELETE CASCADE,
    discipline    text NOT NULL CHECK (discipline IN ('Level', 'Mixed')),
    status        text NOT NULL DEFAULT 'Pending' CHECK (status IN ('Pending', 'Confirmed', 'Rejected')),
    requested_by  uuid NULL REFERENCES users(id) ON DELETE SET NULL,
    responded_by  uuid NULL REFERENCES users(id) ON DELETE SET NULL,
    requested_at  timestamptz NOT NULL DEFAULT now(),
    responded_at  timestamptz NULL
);
CREATE INDEX ix_disc_reg_league ON discipline_registrations (league_id);
CREATE INDEX ix_disc_reg_club ON discipline_registrations (club_id);
CREATE INDEX ix_disc_reg_player ON discipline_registrations (player_id);
-- Exclusivity invariant: at most one Confirmed club per (player, league, discipline).
CREATE UNIQUE INDEX ux_disc_reg_confirmed
    ON discipline_registrations (player_id, league_id, discipline)
    WHERE status = 'Confirmed';
-- At most one outstanding request per (player, club, league, discipline).
CREATE UNIQUE INDEX ux_disc_reg_pending
    ON discipline_registrations (player_id, club_id, league_id, discipline)
    WHERE status = 'Pending';

-- A transfer of a Confirmed registration between clubs within one (league, discipline).
CREATE TABLE registration_transfers (
    id                 uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    player_id          uuid NOT NULL REFERENCES players(id) ON DELETE CASCADE,
    league_id          uuid NOT NULL REFERENCES leagues(id) ON DELETE CASCADE,
    discipline         text NOT NULL CHECK (discipline IN ('Level', 'Mixed')),
    from_club_id       uuid NOT NULL REFERENCES clubs(id) ON DELETE CASCADE,
    to_club_id         uuid NOT NULL REFERENCES clubs(id) ON DELETE CASCADE,
    status             text NOT NULL DEFAULT 'Pending' CHECK (status IN ('Pending', 'Completed', 'Rejected')),
    releasing_approved boolean NOT NULL DEFAULT false,
    league_approved    boolean NOT NULL DEFAULT false,
    initiated_by       uuid NULL REFERENCES users(id) ON DELETE SET NULL,
    created_at         timestamptz NOT NULL DEFAULT now(),
    resolved_at        timestamptz NULL,
    CHECK (from_club_id <> to_club_id)
);
CREATE INDEX ix_reg_transfer_league ON registration_transfers (league_id);
CREATE INDEX ix_reg_transfer_from ON registration_transfers (from_club_id);
CREATE INDEX ix_reg_transfer_to ON registration_transfers (to_club_id);
-- At most one open transfer per (player, league, discipline).
CREATE UNIQUE INDEX ux_reg_transfer_open
    ON registration_transfers (player_id, league_id, discipline)
    WHERE status = 'Pending';
