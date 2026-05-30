namespace smash_dates.Endpoints.SeasonEntries;

public static class SeasonEntryEndpoints
{
    public static IEndpointRouteBuilder MapSeasonEntryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/leagues/{leagueId:guid}/seasons/{seasonId:guid}/entries")
            .RequireAuthorization();

        group.MapCreateSeasonEntryEndpoint();
        group.MapImportSeasonEntriesEndpoint();
        group.MapListSeasonEntriesEndpoint();
        group.MapDeleteSeasonEntryEndpoint();
        return app;
    }
}
