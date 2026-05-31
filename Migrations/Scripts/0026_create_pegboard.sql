-- Club night "pegboard": in-person session tracking, wholly separate from league play.
-- See docs/adr/0004-sse-for-pegboard-live-updates.md and CONTEXT.md "Club Night (Pegboard)".

-- Optional ability grade on global players (1 = strongest .. 5 = weakest). Pegboard-only aid.
ALTER TABLE players ADD COLUMN grade smallint NULL CHECK (grade BETWEEN 1 AND 5);

-- Per-club role grant: may run pegboard sessions and nothing else. No last-host protection.
CREATE TABLE session_hosts (
    club_id     uuid NOT NULL REFERENCES clubs(id) ON DELETE CASCADE,
    user_id     uuid NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    granted_at  timestamptz NOT NULL DEFAULT now(),
    granted_by  uuid NULL REFERENCES users(id) ON DELETE SET NULL,
    PRIMARY KEY (club_id, user_id)
);
CREATE INDEX ix_session_hosts_user ON session_hosts (user_id);

-- One club night, owned by a club. Status Open -> Closed (terminal).
CREATE TABLE pegboard_sessions (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    club_id     uuid NOT NULL REFERENCES clubs(id) ON DELETE CASCADE,
    name        text NOT NULL,
    status      text NOT NULL DEFAULT 'Open' CHECK (status IN ('Open', 'Closed')),
    opened_by   uuid NULL REFERENCES users(id) ON DELETE SET NULL,
    opened_at   timestamptz NOT NULL DEFAULT now(),
    closed_at   timestamptz NULL
);
CREATE INDEX ix_pegboard_sessions_club ON pegboard_sessions (club_id);
-- At most one Open session per club (invariant, enforced in the database).
CREATE UNIQUE INDEX ux_pegboard_session_open_per_club
    ON pegboard_sessions (club_id) WHERE status = 'Open';

-- Courts within a session. Host adds any time; removes only while empty.
CREATE TABLE pegboard_courts (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id  uuid NOT NULL REFERENCES pegboard_sessions(id) ON DELETE CASCADE,
    label       text NOT NULL,
    created_at  timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_pegboard_courts_session ON pegboard_courts (session_id);

-- Attendances (the "pegs"): a roster player OR an ad-hoc guest, never both.
CREATE TABLE pegboard_attendances (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id    uuid NOT NULL REFERENCES pegboard_sessions(id) ON DELETE CASCADE,
    player_id     uuid NULL REFERENCES players(id) ON DELETE RESTRICT,
    guest_name    text NULL,
    gender        text NOT NULL CHECK (gender IN ('Male', 'Female')),
    grade         smallint NULL CHECK (grade BETWEEN 1 AND 5),
    status        text NOT NULL DEFAULT 'Waiting'
                  CHECK (status IN ('Waiting', 'Playing', 'Resting', 'Left')),
    waiting_since timestamptz NOT NULL DEFAULT now(),
    created_at    timestamptz NOT NULL DEFAULT now(),
    CHECK ((player_id IS NULL) <> (guest_name IS NULL))
);
CREATE INDEX ix_pegboard_attendances_session ON pegboard_attendances (session_id);
-- A roster player appears at most once per session.
CREATE UNIQUE INDEX ux_pegboard_attendance_player
    ON pegboard_attendances (session_id, player_id) WHERE player_id IS NOT NULL;

-- Games on a court. Active -> Finished (needs winner) | Cancelled (no result).
CREATE TABLE pegboard_games (
    id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id   uuid NOT NULL REFERENCES pegboard_sessions(id) ON DELETE CASCADE,
    court_id     uuid NOT NULL REFERENCES pegboard_courts(id) ON DELETE CASCADE,
    type         text NOT NULL CHECK (type IN ('Singles', 'Doubles', 'Mixed', 'Funny')),
    status       text NOT NULL DEFAULT 'Active'
                 CHECK (status IN ('Active', 'Finished', 'Cancelled')),
    winner_side  text NULL CHECK (winner_side IN ('A', 'B')),
    score        text NULL,
    started_at   timestamptz NOT NULL DEFAULT now(),
    ended_at     timestamptz NULL,
    CHECK (status <> 'Finished' OR winner_side IS NOT NULL)
);
CREATE INDEX ix_pegboard_games_session ON pegboard_games (session_id);
CREATE INDEX ix_pegboard_games_court ON pegboard_games (court_id);
-- At most one active game per court.
CREATE UNIQUE INDEX ux_pegboard_game_active_per_court
    ON pegboard_games (court_id) WHERE status = 'Active';

-- Which attendances are on which side of a game.
CREATE TABLE pegboard_game_players (
    game_id       uuid NOT NULL REFERENCES pegboard_games(id) ON DELETE CASCADE,
    attendance_id uuid NOT NULL REFERENCES pegboard_attendances(id) ON DELETE RESTRICT,
    side          text NOT NULL CHECK (side IN ('A', 'B')),
    PRIMARY KEY (game_id, attendance_id)
);
CREATE INDEX ix_pegboard_game_players_attendance ON pegboard_game_players (attendance_id);
