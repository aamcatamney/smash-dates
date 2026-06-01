using System.Security.Claims;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Players;

public static class SetPlayerGradeEndpoint
{
    public sealed record SetGradeRequest(int? Grade);

    public static IEndpointRouteBuilder MapSetPlayerGradeEndpoint(this IEndpointRouteBuilder app)
    {
        // Mapped on the existing /api/clubs/{clubId}/players group in ClubPlayersEndpoints.
        app.MapPatch("/{playerId:guid}/grade", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId, Guid playerId, SetGradeRequest request, ClaimsPrincipal principal,
        IClubRepository clubs, IClubAdminRepository admins, IPlayerRepository players, CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(clubId, ct) is null) return Results.NotFound();
        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, clubId, admins, ct);
        if (authz is not null) return authz;
        if (request.Grade is < 1 or > 5)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Grade must be 1-5 or null");

        return await players.SetGradeAsync(playerId, request.Grade, ct)
            ? Results.NoContent() : Results.NotFound();
    }
}
