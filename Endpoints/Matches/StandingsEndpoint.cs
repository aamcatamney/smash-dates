using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Standings;

namespace smash_dates.Endpoints.Matches;

public static class StandingsEndpoint
{
    public sealed record RowDto(
        Guid TeamId, string TeamName, int Played, int Won, int Drawn, int Lost,
        int RubbersFor, int RubbersAgainst, int RubberDifference, int Points);

    public sealed record DivisionTable(Guid DivisionId, string DivisionName, RowDto[] Rows);

    public static IEndpointRouteBuilder MapStandingsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/standings", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid leagueId,
        Guid seasonId,
        IDivisionRepository divisions,
        ISeasonEntryRepository entries,
        IMatchRepository matches,
        CancellationToken ct)
    {
        var divisionList = await divisions.ListByLeagueAsync(leagueId, ct);
        var entryRows = await entries.ListBySeasonAsync(seasonId, ct);
        var played = (await matches.ListBySeasonAsync(seasonId, ct))
            .Where(m => m.Status == MatchStatus.Played)
            .ToList();

        var teamsByDivision = entryRows
            .GroupBy(e => e.DivisionId)
            .ToDictionary(g => g.Key, g => g.Select(e => new StandingTeam(e.TeamId, e.TeamName)).ToList());

        var tables = new List<DivisionTable>();
        foreach (var division in divisionList)
        {
            if (!teamsByDivision.TryGetValue(division.Id, out var teams)) continue; // no teams entered

            var results = played
                .Where(m => m.DivisionId == division.Id && m.HomeScore is not null && m.AwayScore is not null)
                .Select(m => new StandingResult(m.HomeTeamId, m.AwayTeamId, m.HomeScore!.Value, m.AwayScore!.Value))
                .ToList();

            var scheme = new PointsScheme(division.WinPoints, division.DrawPoints, division.LossPoints);
            var rows = StandingsCalculator.Compute(teams, scheme, results)
                .Select(r => new RowDto(
                    r.TeamId, r.TeamName, r.Played, r.Won, r.Drawn, r.Lost,
                    r.RubbersFor, r.RubbersAgainst, r.RubberDifference, r.Points))
                .ToArray();

            tables.Add(new DivisionTable(division.Id, division.Name, rows));
        }

        return Results.Ok(tables);
    }
}
