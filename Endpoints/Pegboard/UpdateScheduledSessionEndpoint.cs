using System.Security.Claims;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Pegboard;

// Edit a session's plan while it is still Scheduled (name, date, time, duration, venue).
public static class UpdateScheduledSessionEndpoint
{
    public sealed record UpdateRequest(
        string Name, DateOnly? ScheduledDate, TimeOnly? StartTime, int? DurationMinutes, Guid? VenueId);

    public static IEndpointRouteBuilder MapUpdateScheduledSessionEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/{sessionId:guid}", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId, Guid sessionId, UpdateRequest request, ClaimsPrincipal principal,
        IClubAdminRepository admins, ISessionHostRepository hosts,
        IVenueRepository venues, IPegboardRepository pegboard, CancellationToken ct)
    {
        var session = await pegboard.GetSessionAsync(sessionId, ct);
        if (session is null || session.ClubId != clubId) return Results.NotFound();

        var authz = await SessionAuthorizer.RequireSessionRunnerAsync(principal, clubId, admins, hosts, ct);
        if (authz is not null) return authz;

        if (session.Status != Models.PegboardSessionStatus.Scheduled)
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Only scheduled sessions can be edited");

        var (name, error) = await ScheduleSessionEndpoint.ValidateAsync(clubId, request.Name, request.ScheduledDate,
            request.DurationMinutes, request.VenueId, venues, ct);
        if (error is not null) return error;

        await pegboard.UpdateScheduledAsync(sessionId, name, request.ScheduledDate!.Value,
            request.StartTime, request.DurationMinutes, request.VenueId, ct);
        return Results.NoContent();
    }
}
