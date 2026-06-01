using System.Security.Claims;
using Npgsql;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Pegboard;

namespace smash_dates.Endpoints.Pegboard;

public static class AddAttendanceEndpoint
{
    private const int MaxName = 100;
    public sealed record AddRequest(Guid? PlayerId, string? GuestName, string? Gender, int? Grade);
    public sealed record AttendanceDto(Guid Id);

    public static IEndpointRouteBuilder MapAddAttendanceEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{sessionId:guid}/attendances", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId, Guid sessionId, AddRequest request, ClaimsPrincipal principal,
        IPegboardRepository pegboard, IClubAdminRepository admins, ISessionHostRepository hosts,
        IPlayerRepository players, IPegboardEventPublisher events, CancellationToken ct)
    {
        var (_, error) = await PegboardGuards.LoadOpenForMutationAsync(clubId, sessionId, principal, pegboard, admins, hosts, ct);
        if (error is not null) return error;

        if (request.Grade is < 1 or > 5)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Grade must be 1-5");

        Guid id;
        if (request.PlayerId is { } playerId)
        {
            var player = await players.GetByIdAsync(playerId, ct);
            if (player is null) return Results.NotFound();
            // Must be affiliated with this club (Member or Visitor).
            if (await players.GetLinkAsync(playerId, clubId, ct) is null)
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Player is not affiliated with this club");
            // Grade defaults to the player's stored grade; request may override for the night.
            var grade = request.Grade ?? player.Grade;
            try
            {
                id = await pegboard.AddPlayerAttendanceAsync(sessionId, playerId, player.Gender, grade, ct);
            }
            catch (PostgresException ex) when (ex.SqlState == "23505")
            {
                return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Player is already on the board");
            }
        }
        else
        {
            var name = (request.GuestName ?? string.Empty).Trim();
            if (name.Length == 0 || name.Length > MaxName)
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Guest name required");
            if (!Enum.TryParse<Gender>(request.Gender, out var gender))
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Guest gender required (Male/Female)");
            id = await pegboard.AddGuestAttendanceAsync(sessionId, name, gender, request.Grade, ct);
        }

        events.Publish(sessionId);
        return Results.Created($"/api/clubs/{clubId}/pegboard/sessions/{sessionId}/attendances/{id}", new AttendanceDto(id));
    }
}
