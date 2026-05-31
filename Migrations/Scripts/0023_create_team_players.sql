-- A Team's persistent squad of Players. Eligibility (the player is Confirmed for the team's
-- discipline at the team's club in a league the team is currently entered in) is enforced by
-- the endpoint at add time; it depends on registrations + entries that change over time, so
-- it is not a static DB constraint. See docs/adr/0003 + CONTEXT.md "Team Squad".
CREATE TABLE team_players (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    team_id     uuid NOT NULL REFERENCES teams(id) ON DELETE CASCADE,
    player_id   uuid NOT NULL REFERENCES players(id) ON DELETE CASCADE,
    created_at  timestamptz NOT NULL DEFAULT now(),
    UNIQUE (team_id, player_id)
);
CREATE INDEX ix_team_players_team ON team_players (team_id);
CREATE INDEX ix_team_players_player ON team_players (player_id);
