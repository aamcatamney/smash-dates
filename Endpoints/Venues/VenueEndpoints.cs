namespace smash_dates.Endpoints.Venues;

public static class VenueEndpoints
{
    public static IEndpointRouteBuilder MapVenueEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/clubs/{clubId:guid}/venues")
            .RequireAuthorization();

        group.MapCreateVenueEndpoint();
        group.MapListVenuesEndpoint();
        group.MapUpdateVenueEndpoint();
        group.MapDeleteVenueEndpoint();
        return app;
    }
}
