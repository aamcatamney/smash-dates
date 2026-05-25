namespace smash_dates.Endpoints.LeagueAdmins;

public static class LeagueAdminEndpoints
{
    public static IEndpointRouteBuilder MapLeagueAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/leagues/{leagueId:guid}/admins")
            .RequireAuthorization();

        group.MapListLeagueAdminsEndpoint();
        group.MapGrantLeagueAdminEndpoint();
        group.MapRevokeLeagueAdminEndpoint();
        return app;
    }
}
