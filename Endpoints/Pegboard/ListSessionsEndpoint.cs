using smash_dates.Repositories;

namespace smash_dates.Endpoints.Pegboard;

public static class ListSessionsEndpoint
{
    public sealed record SessionDto(
        Guid Id, string Name, string Status,
        DateOnly? ScheduledDate, TimeOnly? StartTime, int? DurationMinutes,
        Guid? VenueId, string? VenueName, string? VenueAddress,
        System.DateTime? OpenedAt, System.DateTime? ClosedAt);

    public static IEndpointRouteBuilder MapListSessionsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(Guid clubId, IPegboardRepository pegboard, CancellationToken ct)
    {
        var rows = await pegboard.ListByClubAsync(clubId, ct);
        return Results.Ok(rows.Select(s => new SessionDto(
            s.Id, s.Name, s.Status.ToString(),
            s.ScheduledDate, s.StartTime, s.DurationMinutes,
            s.VenueId, s.VenueName, s.VenueAddress, s.OpenedAt, s.ClosedAt)));
    }
}
