using System.Security.Claims;
using Npgsql;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;
using smash_dates.Services.Pegboard;

namespace smash_dates.Endpoints.Pegboard;

// Auto-rotate fill: the board picks a valid lineup from the waiting queue AND starts the game
// in one step, with no confirm/swap (cf. Suggest, which only proposes). The lineup is valid by
// construction, so there is no makeup warning. See the "Board Fill Modes" entry in CONTEXT.md.
public static class AutoFillEndpoint
{
    public sealed record AutoFillRequest(string Type);
    public sealed record AutoFillResponse(Guid Id);

    public static IEndpointRouteBuilder MapAutoFillEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{sessionId:guid}/courts/{courtId:guid}/auto-fill", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId, Guid sessionId, Guid courtId, AutoFillRequest request, ClaimsPrincipal principal,
        IPegboardRepository pegboard, IClubAdminRepository admins, ISessionHostRepository hosts,
        IPegboardEventPublisher events, CancellationToken ct)
    {
        var (_, error) = await PegboardGuards.LoadOpenForMutationAsync(clubId, sessionId, principal, pegboard, admins, hosts, ct);
        if (error is not null) return error;

        if (!Enum.TryParse<GameType>(request.Type, out var type))
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid game type");

        var court = await pegboard.GetCourtAsync(courtId, ct);
        if (court is null || court.SessionId != sessionId) return Results.NotFound();

        var waiting = await pegboard.ListWaitingAsync(sessionId, ct);
        var pool = waiting.Select((w, i) => new FillCandidate(w.Id, w.Gender, w.Grade, i)).ToList();
        var pairs = await pegboard.ListPlayedPairsAsync(sessionId, ct);

        var fill = PegboardFiller.Suggest(type, pool, pairs);
        if (fill is null)
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Not enough waiting players to form this game");

        try
        {
            var id = await pegboard.StartGameAsync(sessionId, courtId, type, fill.SideA, fill.SideB, ct);
            events.Publish(sessionId);
            return Results.Created($"/api/clubs/{clubId}/pegboard/sessions/{sessionId}/games/{id}",
                new AutoFillResponse(id));
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Court already has an active game");
        }
    }
}
