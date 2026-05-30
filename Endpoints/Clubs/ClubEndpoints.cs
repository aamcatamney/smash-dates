namespace smash_dates.Endpoints.Clubs;

public static class ClubEndpoints
{
    public static IEndpointRouteBuilder MapClubEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/clubs")
            .RequireAuthorization();

        group.MapCreateClubEndpoint();
        group.MapImportClubsEndpoint();
        group.MapListClubsEndpoint();
        group.MapGetClubEndpoint();
        group.MapUpdateClubEndpoint();
        group.MapListClubMatchesEndpoint();
        return app;
    }
}
