namespace smash_dates.Endpoints.Seasons;

public static class SeasonEndpoints
{
    public static IEndpointRouteBuilder MapSeasonEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/leagues/{leagueId:guid}/seasons")
            .RequireAuthorization();

        group.MapCreateSeasonEndpoint();
        group.MapListSeasonsEndpoint();
        group.MapGetSeasonEndpoint();
        group.MapReplaceSeasonWeeksEndpoint();
        group.MapDeleteSeasonEndpoint();
        return app;
    }
}
