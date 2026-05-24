using smash_dates.Repositories;

namespace smash_dates.Endpoints.Leagues;

public static class GetLeagueEndpoint
{
    public sealed record LeagueDetail(Guid Id, string Name, string? Description);

    public static IEndpointRouteBuilder MapGetLeagueEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{id:guid}", Handle);
        return app;
    }

    private static async Task<IResult> Handle(Guid id, ILeagueRepository leagues, CancellationToken ct)
    {
        var league = await leagues.GetByIdAsync(id, ct);
        return league is null
            ? Results.NotFound()
            : Results.Ok(new LeagueDetail(league.Id, league.Name, league.Description));
    }
}
