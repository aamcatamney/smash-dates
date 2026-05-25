namespace smash_dates.Endpoints.Clubs;

public static class ClubEndpoints
{
    public static IEndpointRouteBuilder MapClubEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/clubs")
            .RequireAuthorization();

        group.MapCreateClubEndpoint();
        group.MapListClubsEndpoint();
        group.MapGetClubEndpoint();
        group.MapUpdateClubEndpoint();
        return app;
    }
}
