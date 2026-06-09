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
        IPegboardRepository pegboard, IClubRepository clubs, IClubAdminRepository admins,
        ISessionHostRepository hosts, CancellationToken ct)
    {
        var board = await pegboard.GetBoardAsync(sessionId, ct);
        // BoardView already carries the session; ensure it belongs to this club.
        if (board is null || board.Session.ClubId != clubId) return Results.NotFound();

        // Any authenticated user may read; the runner check only decides whether the client
        // shows host controls. A null result from the authorizer means the caller may manage.
        var canManage = await SessionAuthorizer.RequireSessionRunnerAsync(principal, clubId, admins, hosts, ct) is null;
        // Club identity for the board header (the session row only carries the club id).
        var club = await clubs.GetByIdAsync(clubId, ct);
        return Results.Ok(board with
        {
            CanManage = canManage,
            ClubName = club?.Name ?? "",
            ClubShortCode = club?.ShortCode ?? "",
        });
    }
}
