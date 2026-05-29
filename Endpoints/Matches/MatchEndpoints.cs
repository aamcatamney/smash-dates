namespace smash_dates.Endpoints.Matches;

public static class MatchEndpoints
{
    public static IEndpointRouteBuilder MapMatchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/leagues/{leagueId:guid}/seasons/{seasonId:guid}")
            .RequireAuthorization();

        group.MapGenerateScheduleEndpoint();
        group.MapRerunScheduleEndpoint();
        group.MapListMatchesEndpoint();
        return app;
    }
}
