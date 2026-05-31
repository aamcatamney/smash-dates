using System.Security.Claims;
using smash_dates.Repositories;
using smash_dates.Services.Pegboard;

namespace smash_dates.Endpoints.Pegboard;

public static class RemoveAttendanceEndpoint
{
    public static IEndpointRouteBuilder MapRemoveAttendanceEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/{sessionId:guid}/attendances/{attendanceId:guid}", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId, Guid sessionId, Guid attendanceId, ClaimsPrincipal principal,
        IPegboardRepository pegboard, IClubAdminRepository admins, ISessionHostRepository hosts,
        IPegboardEventPublisher events, CancellationToken ct)
    {
        var (_, error) = await PegboardGuards.LoadOpenForMutationAsync(clubId, sessionId, principal, pegboard, admins, hosts, ct);
        if (error is not null) return error;

        var att = await pegboard.GetAttendanceAsync(attendanceId, ct);
        if (att is null || att.SessionId != sessionId) return Results.NotFound();
        if (await pegboard.IsInActiveGameAsync(attendanceId, ct))
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Attendee is in an active game");

        await pegboard.RemoveAttendanceAsync(attendanceId, ct);
        events.Publish(sessionId);
        return Results.NoContent();
    }
}
