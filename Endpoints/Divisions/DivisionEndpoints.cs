namespace smash_dates.Endpoints.Divisions;

public static class DivisionEndpoints
{
    public static IEndpointRouteBuilder MapDivisionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/leagues/{leagueId:guid}/divisions")
            .RequireAuthorization();

        group.MapCreateDivisionEndpoint();
        group.MapListDivisionsEndpoint();
        return app;
    }
}
