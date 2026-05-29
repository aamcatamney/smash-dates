namespace smash_dates.Endpoints.Teams;

public static class TeamEndpoints
{
    public static IEndpointRouteBuilder MapTeamEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/clubs/{clubId:guid}/teams")
            .RequireAuthorization();

        group.MapCreateTeamEndpoint();
        group.MapListTeamsEndpoint();
        group.MapUpdateTeamEndpoint();
        group.MapDeleteTeamEndpoint();
        return app;
    }
}
