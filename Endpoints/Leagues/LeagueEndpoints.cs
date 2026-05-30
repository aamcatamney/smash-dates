namespace smash_dates.Endpoints.Leagues;

public static class LeagueEndpoints
{
    public static IEndpointRouteBuilder MapLeagueEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/leagues")
            .RequireAuthorization();

        group.MapCreateLeagueEndpoint();
        group.MapListLeaguesEndpoint();
        group.MapGetLeagueEndpoint();
        group.MapSchedulingConfigEndpoints();

        return app;
    }
}
