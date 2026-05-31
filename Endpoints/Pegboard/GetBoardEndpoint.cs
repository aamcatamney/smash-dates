using smash_dates.Repositories;

namespace smash_dates.Endpoints.Pegboard;

public static class GetBoardEndpoint
{
    public static IEndpointRouteBuilder MapGetBoardEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{sessionId:guid}/board", Handle);
        return app;
    }

    private static async Task<IResult> Handle(Guid clubId, Guid sessionId, IPegboardRepository pegboard, CancellationToken ct)
    {
        var board = await pegboard.GetBoardAsync(sessionId, ct);
        // BoardView already carries the session; ensure it belongs to this club.
        return board is null || board.Session.ClubId != clubId ? Results.NotFound() : Results.Ok(board);
    }
}
