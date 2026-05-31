using System.Security.Claims;
using Npgsql;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;
using smash_dates.Services.Pegboard;

namespace smash_dates.Endpoints.Pegboard;

public static class StartGameEndpoint
{
    public sealed record StartRequest(string Type, List<Guid> SideA, List<Guid> SideB);
    public sealed record StartResponse(Guid Id, bool MakeupWarning);

    public static IEndpointRouteBuilder MapStartGameEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{sessionId:guid}/games", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId, Guid sessionId, StartRequest request, ClaimsPrincipal principal,
        IPegboardRepository pegboard, IClubAdminRepository admins, ISessionHostRepository hosts,
        IPegboardEventPublisher events, [Microsoft.AspNetCore.Mvc.FromQuery] Guid courtId, CancellationToken ct)
    {
        var (_, error) = await PegboardGuards.LoadOpenForMutationAsync(clubId, sessionId, principal, pegboard, admins, hosts, ct);
        if (error is not null) return error;

        if (!Enum.TryParse<GameType>(request.Type, out var type))
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid game type");

        var size = GameMakeup.SideSize(type);
        if (request.SideA.Count != size || request.SideB.Count != size)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: $"Each side needs {size} player(s) for {type}");

        var court = await pegboard.GetCourtAsync(courtId, ct);
        if (court is null || court.SessionId != sessionId) return Results.NotFound();

        // All attendances must be Waiting members of this session; collect genders for the makeup check.
        var ids = request.SideA.Concat(request.SideB).ToList();
        if (ids.Distinct().Count() != ids.Count)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "An attendee is listed twice");

        var gendersA = new List<Gender>();
        var gendersB = new List<Gender>();
        foreach (var (sideIds, sink) in new[] { (request.SideA, gendersA), (request.SideB, gendersB) })
        {
            foreach (var id in sideIds)
            {
                var att = await pegboard.GetAttendanceAsync(id, ct);
                if (att is null || att.SessionId != sessionId) return Results.NotFound();
                if (att.Status != AttendanceStatus.Waiting)
                    return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "An attendee is not available");
                sink.Add(att.Gender);
            }
        }

        var warning = !GameMakeup.IsValid(type, gendersA, gendersB);

        try
        {
            var id = await pegboard.StartGameAsync(sessionId, courtId, type, request.SideA, request.SideB, ct);
            events.Publish(sessionId);
            return Results.Created($"/api/clubs/{clubId}/pegboard/sessions/{sessionId}/games/{id}",
                new StartResponse(id, warning));
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Court already has an active game");
        }
    }
}
