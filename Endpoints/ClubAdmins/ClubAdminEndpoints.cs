namespace smash_dates.Endpoints.ClubAdmins;

public static class ClubAdminEndpoints
{
    public static IEndpointRouteBuilder MapClubAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/clubs/{clubId:guid}/admins")
            .RequireAuthorization();

        group.MapListClubAdminsEndpoint();
        group.MapGrantClubAdminEndpoint();
        group.MapRevokeClubAdminEndpoint();
        return app;
    }
}
