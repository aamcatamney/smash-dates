using System.Security.Claims;
using Npgsql;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;
using smash_dates.Services.Notifications;

namespace smash_dates.Endpoints.Players;

// Registration transfer workflow. The receiving club opens a transfer for a player's
// Confirmed registration; the releasing club and the league each approve or reject. Both
// approvals complete it (registration moves; receiving club gains a Member affiliation).
public static class TransferEndpoints
{
    private const string DuplicateSqlState = "23505";

    public sealed record OpenTransferRequest(Guid PlayerId, Guid LeagueId, string Discipline);

    public static IEndpointRouteBuilder MapTransferEndpoints(this IEndpointRouteBuilder app)
    {
        var club = app.MapGroup("/api/clubs/{clubId:guid}/transfers").RequireAuthorization();
        club.MapPost("/", Open);
        club.MapGet("/", ListForClub);
        club.MapGet("/candidates", Candidates);
        club.MapPost("/{id:guid}/approve", ClubApprove);
        club.MapPost("/{id:guid}/reject", ClubReject);

        var league = app.MapGroup("/api/leagues/{leagueId:guid}/transfers").RequireAuthorization();
        league.MapGet("/", ListForLeague);
        league.MapPost("/{id:guid}/approve", LeagueApprove);
        league.MapPost("/{id:guid}/reject", LeagueReject);
        return app;
    }

    private static async Task<IResult> Open(
        Guid clubId,
        OpenTransferRequest request,
        ClaimsPrincipal principal,
        IClubRepository clubs,
        IClubAdminRepository clubAdmins,
        IClubLeagueMembershipRepository memberships,
        IDisciplineRegistrationRepository registrations,
        IRegistrationTransferRepository transfers,
        INotificationService notifications,
        CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(clubId, ct) is null) return Results.NotFound();
        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, clubId, clubAdmins, ct);
        if (authz is not null) return authz;

        if (!Enum.TryParse<Discipline>(request.Discipline, out var discipline))
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Discipline must be Level or Mixed");

        var confirmed = await registrations.GetConfirmedAsync(request.PlayerId, request.LeagueId, discipline, ct);
        if (confirmed is null)
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Player has no confirmed registration for this discipline to transfer");
        if (confirmed.ClubId == clubId)
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Player is already registered at this club");

        if (!await memberships.HasAcceptedMembershipAsync(clubId, request.LeagueId, ct))
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Receiving club is not an accepted member of this league");

        var userId = principal.UserId()!.Value;
        try
        {
            var id = await transfers.CreateAsync(request.PlayerId, request.LeagueId, discipline, confirmed.ClubId, clubId, userId, ct);
            await notifications.TransferOpenedAsync(request.PlayerId, confirmed.ClubId, clubId, request.LeagueId, discipline, ct);
            return Results.Created($"/api/clubs/{clubId}/transfers", new { id });
        }
        catch (PostgresException ex) when (ex.SqlState == DuplicateSqlState)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "A transfer for this registration is already in progress");
        }
    }

    // Candidate players a club may transfer in: Confirmed registrations held by other clubs in a
    // league this club is an Accepted member of, matched by name. Scoped to the club's leagues —
    // it never enumerates players across clubs the receiving club shares no league with.
    private static async Task<IResult> Candidates(
        Guid clubId,
        string? search,
        ClaimsPrincipal principal,
        IClubRepository clubs,
        IClubAdminRepository clubAdmins,
        IDisciplineRegistrationRepository registrations,
        CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(clubId, ct) is null) return Results.NotFound();
        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, clubId, clubAdmins, ct);
        if (authz is not null) return authz;

        var rows = await registrations.SearchTransferCandidatesAsync(clubId, search ?? string.Empty, 20, ct);
        return Results.Ok(rows);
    }

    private static async Task<IResult> ClubApprove(
        Guid clubId, Guid id, ClaimsPrincipal principal,
        IClubAdminRepository clubAdmins, IRegistrationTransferRepository transfers, INotificationService notifications, CancellationToken ct)
    {
        var transfer = await transfers.GetByIdAsync(id, ct);
        if (transfer is null || transfer.FromClubId != clubId) return Results.NotFound();
        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, clubId, clubAdmins, ct);
        if (authz is not null) return authz;

        var both = await transfers.SetReleasingApprovedAsync(id, ct);
        return await ResolveAsync(both, transfer, transfers, notifications, ct);
    }

    private static async Task<IResult> LeagueApprove(
        Guid leagueId, Guid id, ClaimsPrincipal principal,
        ILeagueAdminRepository leagueAdmins, IRegistrationTransferRepository transfers, INotificationService notifications, CancellationToken ct)
    {
        var transfer = await transfers.GetByIdAsync(id, ct);
        if (transfer is null || transfer.LeagueId != leagueId) return Results.NotFound();
        var authz = await LeagueAuthorizer.RequireLeagueAdminAsync(principal, leagueId, leagueAdmins, ct);
        if (authz is not null) return authz;

        var both = await transfers.SetLeagueApprovedAsync(id, ct);
        return await ResolveAsync(both, transfer, transfers, notifications, ct);
    }

    // both == null → was not pending; true → both approvals in, complete it; false → wait.
    private static async Task<IResult> ResolveAsync(bool? both, RegistrationTransfer t, IRegistrationTransferRepository transfers, INotificationService notifications, CancellationToken ct)
    {
        if (both is null) return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Transfer is not pending");
        if (both is true)
        {
            await transfers.CompleteAsync(t, ct);
            await notifications.TransferResolvedAsync(t.PlayerId, t.FromClubId, t.ToClubId, t.LeagueId, t.Discipline, completed: true, ct);
        }
        return Results.NoContent();
    }

    private static async Task<IResult> ClubReject(
        Guid clubId, Guid id, ClaimsPrincipal principal,
        IClubAdminRepository clubAdmins, IRegistrationTransferRepository transfers, INotificationService notifications, CancellationToken ct)
    {
        var transfer = await transfers.GetByIdAsync(id, ct);
        if (transfer is null || transfer.FromClubId != clubId) return Results.NotFound();
        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, clubId, clubAdmins, ct);
        if (authz is not null) return authz;

        return await RejectAndNotify(transfer, transfers, notifications, ct);
    }

    private static async Task<IResult> LeagueReject(
        Guid leagueId, Guid id, ClaimsPrincipal principal,
        ILeagueAdminRepository leagueAdmins, IRegistrationTransferRepository transfers, INotificationService notifications, CancellationToken ct)
    {
        var transfer = await transfers.GetByIdAsync(id, ct);
        if (transfer is null || transfer.LeagueId != leagueId) return Results.NotFound();
        var authz = await LeagueAuthorizer.RequireLeagueAdminAsync(principal, leagueId, leagueAdmins, ct);
        if (authz is not null) return authz;

        return await RejectAndNotify(transfer, transfers, notifications, ct);
    }

    private static async Task<IResult> RejectAndNotify(RegistrationTransfer t, IRegistrationTransferRepository transfers, INotificationService notifications, CancellationToken ct)
    {
        if (!await transfers.RejectAsync(t.Id, ct))
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Transfer is not pending");
        await notifications.TransferResolvedAsync(t.PlayerId, t.FromClubId, t.ToClubId, t.LeagueId, t.Discipline, completed: false, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ListForClub(Guid clubId, IClubRepository clubs, IRegistrationTransferRepository transfers, CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(clubId, ct) is null) return Results.NotFound();
        return Results.Ok(await transfers.ListByClubAsync(clubId, ct));
    }

    private static async Task<IResult> ListForLeague(
        Guid leagueId, ClaimsPrincipal principal,
        ILeagueAdminRepository leagueAdmins, IRegistrationTransferRepository transfers, CancellationToken ct)
    {
        var authz = await LeagueAuthorizer.RequireLeagueAdminAsync(principal, leagueId, leagueAdmins, ct);
        if (authz is not null) return authz;
        return Results.Ok(await transfers.ListByLeagueAsync(leagueId, ct));
    }
}
