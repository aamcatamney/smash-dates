using System.Security.Claims;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Pegboard;

public static class GetBoardEndpoint
{
    public static IEndpointRouteBuilder MapGetBoardEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{sessionId:guid}/board", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId, Guid sessionId, ClaimsPrincipal principal,
        IPegboardRepository pegboard, IClubAdminRepository admins, ISessionHostRepository hosts, CancellationToken ct)
    {
        var board = await pegboard.GetBoardAsync(sessionId, ct);
        // BoardView already carries the session; ensure it belongs to this club.
        if (board is null || board.Session.ClubId != clubId) return Results.NotFound();

        // Any authenticated user may read; the runner check only decides whether the client
        // shows host controls. A null result from the authorizer means the caller may manage.
        var canManage = await SessionAuthorizer.RequireSessionRunnerAsync(principal, clubId, admins, hosts, ct) is null;
        return Results.Ok(board with { CanManage = canManage });
    }
}
