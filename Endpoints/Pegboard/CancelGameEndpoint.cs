using System.Security.Claims;
using smash_dates.Repositories;
using smash_dates.Services.Pegboard;

namespace smash_dates.Endpoints.Pegboard;

public static class CancelGameEndpoint
{
    public static IEndpointRouteBuilder MapCancelGameEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{sessionId:guid}/games/{gameId:guid}/cancel", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId, Guid sessionId, Guid gameId, ClaimsPrincipal principal,
        IPegboardRepository pegboard, IClubAdminRepository admins, ISessionHostRepository hosts,
        IPegboardEventPublisher events, CancellationToken ct)
    {
        var (_, error) = await PegboardGuards.LoadOpenForMutationAsync(clubId, sessionId, principal, pegboard, admins, hosts, ct);
        if (error is not null) return error;

        var game = await pegboard.GetGameAsync(gameId, ct);
        if (game is null || game.SessionId != sessionId) return Results.NotFound();

        var ok = await pegboard.CancelGameAsync(gameId, ct);
        if (!ok) return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Game is not active");
        events.Publish(sessionId);
        return Results.NoContent();
    }
}
