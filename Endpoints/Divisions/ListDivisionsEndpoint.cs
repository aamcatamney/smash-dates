using smash_dates.Repositories;

namespace smash_dates.Endpoints.Divisions;

public static class ListDivisionsEndpoint
{
    public sealed record DivisionSummary(
        Guid Id,
        string Name,
        string Gender,
        int Rank,
        int RubbersPerMatch,
        int WinPoints,
        int DrawPoints,
        int LossPoints);

    public static IEndpointRouteBuilder MapListDivisionsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid leagueId,
        ILeagueRepository leagues,
        IDivisionRepository divisions,
        CancellationToken ct)
    {
        var league = await leagues.GetByIdAsync(leagueId, ct);
        if (league is null) return Results.NotFound();

        var rows = await divisions.ListByLeagueAsync(leagueId, ct);
        var summaries = rows.Select(d => new DivisionSummary(
            d.Id, d.Name, d.Gender.ToString(), d.Rank, d.RubbersPerMatch,
            d.WinPoints, d.DrawPoints, d.LossPoints)).ToArray();
        return Results.Ok(summaries);
    }
}
