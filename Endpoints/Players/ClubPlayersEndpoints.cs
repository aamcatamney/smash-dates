using System.Security.Claims;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Players;

// Player <-> Club affiliations (Member/Visitor) plus a global player search used to link an
// existing player to a second club. Players are global, admin-managed roster records.
public static class ClubPlayersEndpoints
{
    private const int MaxNameLength = 200;

    public sealed record AddClubPlayerRequest(Guid? PlayerId, string? FullName, string? Gender, string Type);
    public sealed record UpdateLinkRequest(string Type);
    public sealed record PlayerDto(Guid Id, string FullName, string Gender, int? Grade);

    public static IEndpointRouteBuilder MapClubPlayersEndpoints(this IEndpointRouteBuilder app)
    {
        var search = app.MapGroup("/api/players").RequireAuthorization();
        search.MapGet("/", SearchPlayers);

        var group = app.MapGroup("/api/clubs/{clubId:guid}/players").RequireAuthorization();
        group.MapGet("/", ListClubPlayers);
        group.MapPost("/", AddClubPlayer);
        group.MapPatch("/{playerId:guid}", UpdateLink);
        group.MapDelete("/{playerId:guid}", Unlink);
        group.MapSetPlayerGradeEndpoint();
        return app;
    }

    // Cross-club search exposes people across every club, so it's limited to club admins
    // (and SystemAdmin) — the users who legitimately link/transfer players.
    private static async Task<IResult> SearchPlayers(
        string? search,
        ClaimsPrincipal principal,
        IPlayerRepository players,
        IClubAdminRepository clubAdmins,
        CancellationToken ct)
    {
        var userId = principal.UserId();
        if (userId is null) return Results.Unauthorized();
        if (!principal.IsSystemAdmin() && !await clubAdmins.IsAdminOfAnyClubAsync(userId.Value, ct))
            return Results.Forbid();

        var rows = await players.SearchAsync(search ?? string.Empty, 20, ct);
        return Results.Ok(rows.Select(p => new PlayerDto(p.Id, p.FullName, p.Gender.ToString(), p.Grade)));
    }

    private static async Task<IResult> ListClubPlayers(Guid clubId, IClubRepository clubs, IPlayerRepository players, CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(clubId, ct) is null) return Results.NotFound();
        return Results.Ok(await players.ListByClubAsync(clubId, ct));
    }

    private static async Task<IResult> AddClubPlayer(
        Guid clubId,
        AddClubPlayerRequest request,
        ClaimsPrincipal principal,
        IClubRepository clubs,
        IPlayerRepository players,
        IClubAdminRepository clubAdmins,
        CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(clubId, ct) is null) return Results.NotFound();
        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, clubId, clubAdmins, ct);
        if (authz is not null) return authz;

        if (!Enum.TryParse<PlayerClubType>(request.Type, out var type))
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Type must be Member or Visitor");

        Guid playerId;
        if (request.PlayerId is { } existingId)
        {
            if (await players.GetByIdAsync(existingId, ct) is null) return Results.NotFound();
            playerId = existingId;
        }
        else
        {
            var name = (request.FullName ?? string.Empty).Trim();
            if (name.Length == 0 || name.Length > MaxNameLength)
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid full name");
            if (!Enum.TryParse<Gender>(request.Gender, out var gender))
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Gender must be Male or Female");
            playerId = await players.CreateAsync(name, gender, ct);
        }

        await players.LinkAsync(playerId, clubId, type, ct);
        var player = await players.GetByIdAsync(playerId, ct)!;
        return Results.Created($"/api/clubs/{clubId}/players", new PlayerDto(playerId, player!.FullName, player.Gender.ToString(), player.Grade));
    }

    private static async Task<IResult> UpdateLink(
        Guid clubId,
        Guid playerId,
        UpdateLinkRequest request,
        ClaimsPrincipal principal,
        IClubRepository clubs,
        IPlayerRepository players,
        IClubAdminRepository clubAdmins,
        CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(clubId, ct) is null) return Results.NotFound();
        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, clubId, clubAdmins, ct);
        if (authz is not null) return authz;

        if (!Enum.TryParse<PlayerClubType>(request.Type, out var type))
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Type must be Member or Visitor");
        if (await players.GetLinkAsync(playerId, clubId, ct) is null) return Results.NotFound();

        await players.LinkAsync(playerId, clubId, type, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> Unlink(
        Guid clubId,
        Guid playerId,
        ClaimsPrincipal principal,
        IClubRepository clubs,
        IPlayerRepository players,
        IClubAdminRepository clubAdmins,
        IDisciplineRegistrationRepository registrations,
        CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(clubId, ct) is null) return Results.NotFound();
        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, clubId, clubAdmins, ct);
        if (authz is not null) return authz;

        var active = (await registrations.ListByClubAsync(clubId, ct))
            .Any(r => r.PlayerId == playerId && r.Status != RegistrationStatus.Rejected);
        if (active)
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Player has registrations at this club; remove those first");

        return await players.UnlinkAsync(playerId, clubId, ct) ? Results.NoContent() : Results.NotFound();
    }
}
