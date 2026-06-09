using System.Security.Claims;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Pegboard;

// Drop a Scheduled session that is no longer wanted. Open/Closed sessions are never deleted
// (closing is the terminal path and Closed sessions are retained as history).
public static class DeleteScheduledSessionEndpoint
{
    public static IEndpointRouteBuilder MapDeleteScheduledSessionEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/{sessionId:guid}", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId, Guid sessionId, ClaimsPrincipal principal,
        IClubAdminRepository admins, ISessionHostRepository hosts,
        IPegboardRepository pegboard, CancellationToken ct)
    {
        var session = await pegboard.GetSessionAsync(sessionId, ct);
        if (session is null || session.ClubId != clubId) return Results.NotFound();

        var authz = await SessionAuthorizer.RequireSessionRunnerAsync(principal, clubId, admins, hosts, ct);
        if (authz is not null) return authz;

        if (session.Status != Models.PegboardSessionStatus.Scheduled)
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Only scheduled sessions can be deleted");

        await pegboard.DeleteScheduledAsync(sessionId, ct);
        return Results.NoContent();
    }
}
