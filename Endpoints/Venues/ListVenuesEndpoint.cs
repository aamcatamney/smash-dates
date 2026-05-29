using smash_dates.Repositories;

namespace smash_dates.Endpoints.Venues;

public static class ListVenuesEndpoint
{
    public sealed record VenueSummary(Guid Id, Guid ClubId, string Name, int Capacity);

    public static IEndpointRouteBuilder MapListVenuesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(Guid clubId, IVenueRepository venues, CancellationToken ct)
    {
        var rows = await venues.ListByClubAsync(clubId, ct);
        return Results.Ok(rows.Select(v => new VenueSummary(v.Id, v.ClubId, v.Name, v.Capacity)).ToArray());
    }
}
