using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Standings;

namespace smash_dates.Endpoints.Public;

// Anonymous, read-only public view of leagues: standings + fixtures, no login. Deliberately
// PII-free — only team / division / venue names, dates and scores are exposed, never the
// admin-facing club contact details or membership/accept state. See issue #65.
public static class PublicEndpoints
{
    public sealed record LeagueDto(Guid Id, string Name, string? Description);
    public sealed record SeasonDto(Guid Id, string Name, DateOnly StartDate, DateOnly EndDate, string Status);
    public sealed record LeagueDetailDto(Guid Id, string Name, string? Description, SeasonDto[] Seasons);
    public sealed record StandingRowDto(
        Guid TeamId, string TeamName, int Played, int Won, int Drawn, int Lost,
        int RubbersFor, int RubbersAgainst, int RubberDifference, int Points);
    public sealed record StandingsTableDto(Guid DivisionId, string DivisionName, StandingRowDto[] Rows);
    public sealed record FixtureDto(
        Guid Id, string DivisionName, string HomeTeamName, string AwayTeamName, string VenueName,
        DateOnly MatchDate, string Status, int? HomeScore, int? AwayScore, bool IsWalkover);

    // Seasons worth showing publicly — i.e. ones a schedule has been built for.
    private static readonly SeasonStatus[] PublicSeasonStatuses =
        [SeasonStatus.Proposed, SeasonStatus.Active, SeasonStatus.Closed];

    public static IEndpointRouteBuilder MapPublicEndpoints(this IEndpointRouteBuilder app)
    {
        // No RequireAuthorization: this group is intentionally anonymous.
        var group = app.MapGroup("/api/public");
        group.MapGet("/leagues", ListLeagues);
        group.MapGet("/leagues/{leagueId:guid}", GetLeague);
        group.MapGet("/leagues/{leagueId:guid}/seasons/{seasonId:guid}/standings", Standings);
        group.MapGet("/leagues/{leagueId:guid}/seasons/{seasonId:guid}/fixtures", Fixtures);
        return app;
    }

    private static async Task<IResult> ListLeagues(ILeagueRepository leagues, CancellationToken ct)
    {
        var rows = await leagues.ListAsync(ct);
        return Results.Ok(rows.Select(l => new LeagueDto(l.Id, l.Name, l.Description)).ToArray());
    }

    private static async Task<IResult> GetLeague(
        Guid leagueId, ILeagueRepository leagues, ISeasonRepository seasons, CancellationToken ct)
    {
        var league = await leagues.GetByIdAsync(leagueId, ct);
        if (league is null) return Results.NotFound();

        var seasonDtos = (await seasons.ListByLeagueAsync(leagueId, ct))
            .Where(s => PublicSeasonStatuses.Contains(s.Status))
            .Select(s => new SeasonDto(s.Id, s.Name, s.StartDate, s.EndDate, s.Status.ToString()))
            .ToArray();

        return Results.Ok(new LeagueDetailDto(league.Id, league.Name, league.Description, seasonDtos));
    }

    private static async Task<IResult> Standings(
        Guid leagueId, Guid seasonId,
        ISeasonRepository seasons, IDivisionRepository divisions, ISeasonEntryRepository entries,
        IMatchRepository matches, CancellationToken ct)
    {
        if (await SeasonInLeague(seasons, leagueId, seasonId, ct) is null) return Results.NotFound();

        // Mirrors the authenticated standings endpoint, sharing StandingsCalculator.
        var divisionList = await divisions.ListByLeagueAsync(leagueId, ct);
        var entryRows = await entries.ListBySeasonAsync(seasonId, ct);
        var played = (await matches.ListBySeasonAsync(seasonId, ct))
            .Where(m => m.Status == MatchStatus.Played && m.HomeScore is not null && m.AwayScore is not null)
            .ToList();

        var teamsByDivision = entryRows
            .GroupBy(e => e.DivisionId)
            .ToDictionary(g => g.Key, g => g.Select(e => new StandingTeam(e.TeamId, e.TeamName)).ToList());

        var tables = new List<StandingsTableDto>();
        foreach (var division in divisionList)
        {
            if (!teamsByDivision.TryGetValue(division.Id, out var teams)) continue;

            var results = played
                .Where(m => m.DivisionId == division.Id)
                .Select(m => new StandingResult(m.HomeTeamId, m.AwayTeamId, m.HomeScore!.Value, m.AwayScore!.Value))
                .ToList();

            var scheme = new PointsScheme(division.WinPoints, division.DrawPoints, division.LossPoints);
            var rows = StandingsCalculator.Compute(teams, scheme, results)
                .Select(r => new StandingRowDto(
                    r.TeamId, r.TeamName, r.Played, r.Won, r.Drawn, r.Lost,
                    r.RubbersFor, r.RubbersAgainst, r.RubberDifference, r.Points))
                .ToArray();

            tables.Add(new StandingsTableDto(division.Id, division.Name, rows));
        }

        return Results.Ok(tables);
    }

    private static async Task<IResult> Fixtures(
        Guid leagueId, Guid seasonId, ISeasonRepository seasons, IMatchRepository matches, CancellationToken ct)
    {
        if (await SeasonInLeague(seasons, leagueId, seasonId, ct) is null) return Results.NotFound();

        var rows = (await matches.ListBySeasonAsync(seasonId, ct))
            .Where(m => m.Status != MatchStatus.Rejected) // a rejected proposal isn't a real fixture
            .OrderBy(m => m.MatchDate)
            .ThenBy(m => m.DivisionName)
            .Select(m => new FixtureDto(
                m.Id, m.DivisionName, m.HomeTeamName, m.AwayTeamName, m.VenueName, m.MatchDate,
                m.Status.ToString(), m.HomeScore, m.AwayScore, m.IsWalkover))
            .ToArray();

        return Results.Ok(rows);
    }

    private static async Task<Season?> SeasonInLeague(
        ISeasonRepository seasons, Guid leagueId, Guid seasonId, CancellationToken ct)
    {
        var season = await seasons.GetByIdAsync(seasonId, ct);
        return season is not null && season.LeagueId == leagueId ? season : null;
    }
}
