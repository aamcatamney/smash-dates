using smash_dates.Repositories;

namespace smash_dates.Endpoints.Seasons;

public static class ListSeasonsEndpoint
{
    public sealed record SeasonSummary(
        Guid Id, Guid LeagueId, string Name, DateOnly StartDate, DateOnly EndDate, string Status);

    public static IEndpointRouteBuilder MapListSeasonsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(Guid leagueId, ISeasonRepository seasons, CancellationToken ct)
    {
        var rows = await seasons.ListByLeagueAsync(leagueId, ct);
        return Results.Ok(rows
            .Select(s => new SeasonSummary(s.Id, s.LeagueId, s.Name, s.StartDate, s.EndDate, s.Status.ToString()))
            .ToArray());
    }
}
