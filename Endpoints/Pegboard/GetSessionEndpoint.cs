using smash_dates.Repositories;

namespace smash_dates.Endpoints.Pegboard;

public static class GetSessionEndpoint
{
    public sealed record SessionDto(
        Guid Id, Guid ClubId, string Name, string Status,
        DateOnly? ScheduledDate, TimeOnly? StartTime, int? DurationMinutes, Guid? VenueId,
        System.DateTime? OpenedAt, System.DateTime? ClosedAt);

    public static IEndpointRouteBuilder MapGetSessionEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{sessionId:guid}", Handle);
        return app;
    }

    private static async Task<IResult> Handle(Guid clubId, Guid sessionId, IPegboardRepository pegboard, CancellationToken ct)
    {
        var s = await pegboard.GetSessionAsync(sessionId, ct);
        return s is null || s.ClubId != clubId
            ? Results.NotFound()
            : Results.Ok(new SessionDto(
                s.Id, s.ClubId, s.Name, s.Status.ToString(),
                s.ScheduledDate, s.StartTime, s.DurationMinutes, s.VenueId, s.OpenedAt, s.ClosedAt));
    }
}
