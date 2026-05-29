using smash_dates.Repositories;

namespace smash_dates.Endpoints.SeasonEntries;

public static class ListSeasonEntriesEndpoint
{
    public sealed record EntrySummary(
        Guid Id, Guid SeasonId, Guid DivisionId, string DivisionName, Guid TeamId, string TeamName, string Gender);

    public static IEndpointRouteBuilder MapListSeasonEntriesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(Guid seasonId, ISeasonEntryRepository entries, CancellationToken ct)
    {
        var rows = await entries.ListBySeasonAsync(seasonId, ct);
        return Results.Ok(rows
            .Select(e => new EntrySummary(
                e.Id, e.SeasonId, e.DivisionId, e.DivisionName, e.TeamId, e.TeamName, e.Gender.ToString()))
            .ToArray());
    }
}
