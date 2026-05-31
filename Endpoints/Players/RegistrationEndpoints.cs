using System.Security.Claims;
using Npgsql;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;
using smash_dates.Services.Notifications;

namespace smash_dates.Endpoints.Players;

// Discipline registration workflow: a club requests a registration for one of its Members,
// the league confirms or rejects. Exclusivity (one confirmed club per player/league/discipline)
// is enforced by a partial unique index; a clash surfaces as 409 (use a transfer instead).
public static class RegistrationEndpoints
{
    private const string DuplicateSqlState = "23505";

    public sealed record RequestRegistrationRequest(Guid LeagueId, string Discipline);

    public static IEndpointRouteBuilder MapRegistrationEndpoints(this IEndpointRouteBuilder app)
    {
        var club = app.MapGroup("/api/clubs/{clubId:guid}").RequireAuthorization();
        club.MapPost("/players/{playerId:guid}/registrations", Request);
        club.MapGet("/registrations", ListForClub);

        var league = app.MapGroup("/api/leagues/{leagueId:guid}/registrations").RequireAuthorization();
        league.MapGet("/", ListForLeague);
        league.MapPost("/{id:guid}/confirm", Confirm);
        league.MapPost("/{id:guid}/reject", Reject);
        return app;
    }

    private static async Task<IResult> Request(
        Guid clubId,
        Guid playerId,
        RequestRegistrationRequest request,
        ClaimsPrincipal principal,
        IClubRepository clubs,
        IPlayerRepository players,
        IClubAdminRepository clubAdmins,
        IClubLeagueMembershipRepository memberships,
        IDisciplineRegistrationRepository registrations,
        INotificationService notifications,
        CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(clubId, ct) is null) return Results.NotFound();
        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, clubId, clubAdmins, ct);
        if (authz is not null) return authz;

        if (!Enum.TryParse<Discipline>(request.Discipline, out var discipline))
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Discipline must be Level or Mixed");

        var link = await players.GetLinkAsync(playerId, clubId, ct);
        if (link is null) return Results.NotFound();
        if (link.Type != PlayerClubType.Member)
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Only Members can be registered; this player is a Visitor");

        if (!await memberships.HasAcceptedMembershipAsync(clubId, request.LeagueId, ct))
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Club is not an accepted member of this league");

        var confirmed = await registrations.GetConfirmedAsync(playerId, request.LeagueId, discipline, ct);
        if (confirmed is not null)
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: confirmed.ClubId == clubId
                    ? "Player is already registered for this discipline at this club"
                    : "Player is registered for this discipline at another club; use a transfer");

        var userId = principal.UserId()!.Value;
        try
        {
            var id = await registrations.CreateAsync(playerId, clubId, request.LeagueId, discipline, userId, ct);
            await notifications.RegistrationRequestedAsync(playerId, clubId, request.LeagueId, discipline, ct);
            return Results.Created($"/api/clubs/{clubId}/registrations", new { id });
        }
        catch (PostgresException ex) when (ex.SqlState == DuplicateSqlState)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "A pending request already exists");
        }
    }

    private static async Task<IResult> ListForClub(Guid clubId, IClubRepository clubs, IDisciplineRegistrationRepository registrations, CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(clubId, ct) is null) return Results.NotFound();
        return Results.Ok(await registrations.ListByClubAsync(clubId, ct));
    }

    private static async Task<IResult> ListForLeague(
        Guid leagueId,
        ClaimsPrincipal principal,
        ILeagueAdminRepository leagueAdmins,
        IDisciplineRegistrationRepository registrations,
        CancellationToken ct)
    {
        var authz = await LeagueAuthorizer.RequireLeagueAdminAsync(principal, leagueId, leagueAdmins, ct);
        if (authz is not null) return authz;
        return Results.Ok(await registrations.ListByLeagueAsync(leagueId, ct));
    }

    private static async Task<IResult> Confirm(
        Guid leagueId,
        Guid id,
        ClaimsPrincipal principal,
        ILeagueAdminRepository leagueAdmins,
        IDisciplineRegistrationRepository registrations,
        INotificationService notifications,
        CancellationToken ct)
    {
        var authz = await LeagueAuthorizer.RequireLeagueAdminAsync(principal, leagueId, leagueAdmins, ct);
        if (authz is not null) return authz;

        var reg = await registrations.GetByIdAsync(id, ct);
        if (reg is null || reg.LeagueId != leagueId) return Results.NotFound();

        var userId = principal.UserId()!.Value;
        try
        {
            if (!await registrations.ConfirmAsync(id, userId, ct))
                return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Registration is not pending");
            await notifications.RegistrationRespondedAsync(reg.PlayerId, reg.ClubId, leagueId, reg.Discipline, RegistrationStatus.Confirmed, ct);
            return Results.NoContent();
        }
        catch (PostgresException ex) when (ex.SqlState == DuplicateSqlState)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Another club already holds this registration; a transfer is required");
        }
    }

    private static async Task<IResult> Reject(
        Guid leagueId,
        Guid id,
        ClaimsPrincipal principal,
        ILeagueAdminRepository leagueAdmins,
        IDisciplineRegistrationRepository registrations,
        INotificationService notifications,
        CancellationToken ct)
    {
        var authz = await LeagueAuthorizer.RequireLeagueAdminAsync(principal, leagueId, leagueAdmins, ct);
        if (authz is not null) return authz;

        var reg = await registrations.GetByIdAsync(id, ct);
        if (reg is null || reg.LeagueId != leagueId) return Results.NotFound();

        var userId = principal.UserId()!.Value;
        if (!await registrations.RejectAsync(id, userId, ct))
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Registration is not pending");
        await notifications.RegistrationRespondedAsync(reg.PlayerId, reg.ClubId, leagueId, reg.Discipline, RegistrationStatus.Rejected, ct);
        return Results.NoContent();
    }
}
