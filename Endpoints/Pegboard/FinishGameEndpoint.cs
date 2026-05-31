using System.Security.Claims;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Pegboard;

namespace smash_dates.Endpoints.Pegboard;

public static class FinishGameEndpoint
{
    private const int MaxScore = 30;
    public sealed record FinishRequest(string WinnerSide, string? Score);

    public static IEndpointRouteBuilder MapFinishGameEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{sessionId:guid}/games/{gameId:guid}/finish", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId, Guid sessionId, Guid gameId, FinishRequest request, ClaimsPrincipal principal,
        IPegboardRepository pegboard, IClubAdminRepository admins, ISessionHostRepository hosts,
        IPegboardEventPublisher events, CancellationToken ct)
    {
        var (_, error) = await PegboardGuards.LoadOpenForMutationAsync(clubId, sessionId, principal, pegboard, admins, hosts, ct);
        if (error is not null) return error;

        if (!Enum.TryParse<GameSide>(request.WinnerSide, out var winner))
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "WinnerSide must be A or B");
        var score = string.IsNullOrWhiteSpace(request.Score) ? null : request.Score!.Trim();
        if (score is { Length: > MaxScore })
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Score too long");

        var game = await pegboard.GetGameAsync(gameId, ct);
        if (game is null || game.SessionId != sessionId) return Results.NotFound();

        var ok = await pegboard.FinishGameAsync(gameId, winner, score, ct);
        if (!ok) return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Game is not active");
        events.Publish(sessionId);
        return Results.NoContent();
    }
}
