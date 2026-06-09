using System.Security.Claims;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Players;

// Edit a roster player's details: full name (typo fixes) and stored grade. Gender is
// immutable. The player is a global record, so a rename shows everywhere the player is
// affiliated — accepted, per the no-dedup model (ADR-0007). Caller must be an admin of a
// club the player is affiliated with.
public static class UpdateClubPlayerDetailsEndpoint
{
    private const int MaxNameLength = 200;

    public sealed record UpdateDetailsRequest(string FullName, int? Grade);

    public static IEndpointRouteBuilder MapUpdateClubPlayerDetailsEndpoint(this IEndpointRouteBuilder app)
    {
        // Mapped on the existing /api/clubs/{clubId}/players group in ClubPlayersEndpoints.
        app.MapPatch("/{playerId:guid}/details", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId, Guid playerId, UpdateDetailsRequest request, ClaimsPrincipal principal,
        IClubRepository clubs, IClubAdminRepository admins, IPlayerRepository players, CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(clubId, ct) is null) return Results.NotFound();
        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, clubId, admins, ct);
        if (authz is not null) return authz;

        var name = (request.FullName ?? string.Empty).Trim();
        if (name.Length == 0 || name.Length > MaxNameLength)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid full name");
        if (request.Grade is < 1 or > 5)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Grade must be 1-5 or null");

        // Only players actually on this club's roster are editable through it.
        if (await players.GetLinkAsync(playerId, clubId, ct) is null) return Results.NotFound();

        await players.UpdateNameAsync(playerId, name, ct);
        await players.SetGradeAsync(playerId, request.Grade, ct);
        return Results.NoContent();
    }
}
