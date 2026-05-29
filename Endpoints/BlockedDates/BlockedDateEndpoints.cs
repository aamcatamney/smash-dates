namespace smash_dates.Endpoints.BlockedDates;

public static class BlockedDateEndpoints
{
    public static IEndpointRouteBuilder MapBlockedDateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/clubs/{clubId:guid}/blocked-dates")
            .RequireAuthorization();

        group.MapCreateBlockedDateEndpoint();
        group.MapListBlockedDatesEndpoint();
        group.MapDeleteBlockedDateEndpoint();
        return app;
    }
}
