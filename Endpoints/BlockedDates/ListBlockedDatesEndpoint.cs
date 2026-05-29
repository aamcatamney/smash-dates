using smash_dates.Repositories;

namespace smash_dates.Endpoints.BlockedDates;

public static class ListBlockedDatesEndpoint
{
    public sealed record BlockedDateSummary(
        Guid Id, Guid ClubId, string Scope, Guid? VenueId, Guid? TeamId,
        DateOnly StartDate, DateOnly EndDate, string Reason);

    public static IEndpointRouteBuilder MapListBlockedDatesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(Guid clubId, IBlockedDateRepository blockedDates, CancellationToken ct)
    {
        var rows = await blockedDates.ListByClubAsync(clubId, ct);
        return Results.Ok(rows
            .Select(b => new BlockedDateSummary(
                b.Id, b.ClubId, b.Scope.ToString(), b.VenueId, b.TeamId, b.StartDate, b.EndDate, b.Reason))
            .ToArray());
    }
}
