using System.Security.Claims;
using smash_dates.Repositories;
using smash_dates.Services.Pegboard;

namespace smash_dates.Endpoints.Pegboard;

public static class RemoveCourtEndpoint
{
    public static IEndpointRouteBuilder MapRemoveCourtEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/{sessionId:guid}/courts/{courtId:guid}", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId, Guid sessionId, Guid courtId, ClaimsPrincipal principal,
        IPegboardRepository pegboard, IClubAdminRepository admins, ISessionHostRepository hosts,
        IPegboardEventPublisher events, CancellationToken ct)
    {
        var (_, error) = await PegboardGuards.LoadOpenForMutationAsync(clubId, sessionId, principal, pegboard, admins, hosts, ct);
        if (error is not null) return error;

        var court = await pegboard.GetCourtAsync(courtId, ct);
        if (court is null || court.SessionId != sessionId) return Results.NotFound();
        if (await pegboard.HasActiveGameOnCourtAsync(courtId, ct))
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Court has an active game");

        await pegboard.RemoveCourtAsync(courtId, ct);
        events.Publish(sessionId);
        return Results.NoContent();
    }
}
