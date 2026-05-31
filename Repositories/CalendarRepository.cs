using Dapper;
using smash_dates.Data;

namespace smash_dates.Repositories;

public sealed class CalendarRepository : ICalendarRepository
{
    // Only scheduled fixtures (not Rejected/Postponed) appear in feeds.
    private const string Select =
        @"SELECT m.id, m.match_date, m.status,
                 ht.name AS home_team_name, at.name AS away_team_name,
                 v.name AS venue_name, d.name AS division_name, l.name AS league_name
          FROM matches m
          JOIN teams ht ON ht.id = m.home_team_id
          JOIN teams at ON at.id = m.away_team_id
          JOIN venues v ON v.id = m.venue_id
          JOIN divisions d ON d.id = m.division_id
          JOIN seasons s ON s.id = m.season_id
          JOIN leagues l ON l.id = s.league_id
          WHERE m.status IN ('Proposed', 'Confirmed', 'Played')";

    private readonly IDbConnectionFactory _factory;

    public CalendarRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public Task<IReadOnlyList<CalendarMatch>> ListByClubAsync(Guid clubId, CancellationToken ct = default) =>
        QueryAsync($"{Select} AND (ht.club_id = @id OR at.club_id = @id) ORDER BY m.match_date", clubId, ct);

    public Task<IReadOnlyList<CalendarMatch>> ListByLeagueAsync(Guid leagueId, CancellationToken ct = default) =>
        QueryAsync($"{Select} AND s.league_id = @id ORDER BY m.match_date", leagueId, ct);

    public Task<IReadOnlyList<CalendarMatch>> ListByTeamAsync(Guid teamId, CancellationToken ct = default) =>
        QueryAsync($"{Select} AND (m.home_team_id = @id OR m.away_team_id = @id) ORDER BY m.match_date", teamId, ct);

    private async Task<IReadOnlyList<CalendarMatch>> QueryAsync(string sql, Guid id, CancellationToken ct)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<CalendarMatch>(new CommandDefinition(sql, new { id }, cancellationToken: ct));
        return rows.AsList();
    }
}
