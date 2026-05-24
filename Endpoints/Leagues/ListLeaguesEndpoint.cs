using smash_dates.Repositories;

namespace smash_dates.Endpoints.Leagues;

public static class ListLeaguesEndpoint
{
    public sealed record LeagueSummary(Guid Id, string Name, string? Description);

    public static IEndpointRouteBuilder MapListLeaguesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(ILeagueRepository leagues, CancellationToken ct)
    {
        var rows = await leagues.ListAsync(ct);
        var summaries = rows.Select(l => new LeagueSummary(l.Id, l.Name, l.Description)).ToArray();
        return Results.Ok(summaries);
    }
}
