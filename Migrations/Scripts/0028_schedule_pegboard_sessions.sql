-- Scheduled club nights: a session can now be planned ahead of time and opened later.
-- Lifecycle gains a leading state: Scheduled -> Open -> Closed (see ADR-0008).
-- A Scheduled session has a planned date (and optional start time, duration and venue) but
-- is not yet live; a host opens it when the night begins. Many sessions may be Scheduled at
-- once, but the "at most one Open per club" invariant (ux_pegboard_session_open_per_club) is
-- unchanged, so only one can be live.

-- Planning fields. Time, duration and venue are optional; the date defines a Scheduled session.
ALTER TABLE pegboard_sessions
    ADD COLUMN scheduled_date    date NULL,
    ADD COLUMN start_time        time NULL,
    ADD COLUMN duration_minutes  int  NULL CHECK (duration_minutes IS NULL OR duration_minutes > 0),
    ADD COLUMN venue_id          uuid NULL REFERENCES venues(id) ON DELETE SET NULL;

-- A Scheduled session has no open time yet; it is set when the session is opened.
ALTER TABLE pegboard_sessions ALTER COLUMN opened_at DROP NOT NULL;
ALTER TABLE pegboard_sessions ALTER COLUMN opened_at DROP DEFAULT;

-- Allow the new leading state.
ALTER TABLE pegboard_sessions DROP CONSTRAINT pegboard_sessions_status_check;
ALTER TABLE pegboard_sessions
    ADD CONSTRAINT pegboard_sessions_status_check CHECK (status IN ('Scheduled', 'Open', 'Closed'));

-- Keep the states coherent: a Scheduled session is dated and not yet opened; an Open/Closed
-- session has been opened. (Existing Open/Closed rows already have opened_at set.)
ALTER TABLE pegboard_sessions
    ADD CONSTRAINT pegboard_sessions_lifecycle_check CHECK (
        (status = 'Scheduled' AND scheduled_date IS NOT NULL AND opened_at IS NULL)
        OR (status IN ('Open', 'Closed') AND opened_at IS NOT NULL)
    );

CREATE INDEX ix_pegboard_sessions_scheduled
    ON pegboard_sessions (club_id, scheduled_date) WHERE status = 'Scheduled';
