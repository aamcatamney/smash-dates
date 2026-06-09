using System.Security.Claims;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Pegboard;

// Plan a club night ahead of time. Creates a Scheduled session (date required; start time,
// duration and venue optional). A host opens it on the night via POST /{sessionId}/open.
public static class ScheduleSessionEndpoint
{
    public const int MaxNameLength = 200;
    public sealed record ScheduleRequest(
        string Name, DateOnly? ScheduledDate, TimeOnly? StartTime, int? DurationMinutes, Guid? VenueId);
    public sealed record SessionDto(Guid Id, Guid ClubId, string Name, string Status);

    public static IEndpointRouteBuilder MapScheduleSessionEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/scheduled", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId, ScheduleRequest request, ClaimsPrincipal principal,
        IClubRepository clubs, IClubAdminRepository admins, ISessionHostRepository hosts,
        IVenueRepository venues, IPegboardRepository pegboard, CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(clubId, ct) is null) return Results.NotFound();
        var authz = await SessionAuthorizer.RequireSessionRunnerAsync(principal, clubId, admins, hosts, ct);
        if (authz is not null) return authz;

        var (name, error) = await ValidateAsync(clubId, request.Name, request.ScheduledDate,
            request.DurationMinutes, request.VenueId, venues, ct);
        if (error is not null) return error;

        var id = await pegboard.ScheduleAsync(clubId, name, request.ScheduledDate!.Value,
            request.StartTime, request.DurationMinutes, request.VenueId, ct);
        return Results.Created($"/api/clubs/{clubId}/pegboard/sessions/{id}",
            new SessionDto(id, clubId, name, "Scheduled"));
    }

    // Shared by schedule + edit: trims/validates the name, requires a date, a positive duration,
    // and a venue that belongs to this club. Returns the cleaned name, or a populated error result.
    internal static async Task<(string Name, IResult? Error)> ValidateAsync(
        Guid clubId, string? rawName, DateOnly? scheduledDate, int? durationMinutes, Guid? venueId,
        IVenueRepository venues, CancellationToken ct)
    {
        var name = (rawName ?? string.Empty).Trim();
        if (name.Length == 0 || name.Length > MaxNameLength)
            return (name, Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid name"));
        if (scheduledDate is null)
            return (name, Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "A scheduled date is required"));
        if (durationMinutes is <= 0)
            return (name, Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Duration must be positive"));
        if (venueId is { } vid)
        {
            var venue = await venues.GetByIdAsync(vid, ct);
            if (venue is null || venue.ClubId != clubId)
                return (name, Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Venue is not part of this club"));
        }
        return (name, null);
    }
}
