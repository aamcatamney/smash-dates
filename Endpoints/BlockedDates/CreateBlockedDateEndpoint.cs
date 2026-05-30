using System.Security.Claims;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.BlockedDates;

public static class CreateBlockedDateEndpoint
{
    private const int MaxReasonLength = 500;

    public sealed record CreateBlockedDateRequest(
        string Scope,
        Guid? VenueId,
        Guid? TeamId,
        DateOnly StartDate,
        DateOnly EndDate,
        string Reason);

    public sealed record BlockedDateResponse(
        Guid Id, Guid ClubId, string Scope, Guid? VenueId, Guid? TeamId,
        DateOnly StartDate, DateOnly EndDate, string Reason);

    public static IEndpointRouteBuilder MapCreateBlockedDateEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId,
        CreateBlockedDateRequest request,
        ClaimsPrincipal principal,
        IClubRepository clubs,
        IVenueRepository venues,
        ITeamRepository teams,
        IBlockedDateRepository blockedDates,
        IClubAdminRepository clubAdmins,
        ISeasonEntryRepository seasonEntries,
        CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(clubId, ct) is null) return Results.NotFound();

        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, clubId, clubAdmins, ct);
        if (authz is not null) return authz;

        // From Active onward blocked-date edits are forbidden (CONTEXT.md).
        if (await seasonEntries.ClubHasActiveSeasonEntryAsync(clubId, ct))
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Blocked dates can't be changed while the club is in an active season");

        if (!Enum.TryParse<BlockedDateScope>(request.Scope, ignoreCase: false, out var scope))
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid scope");

        var reason = (request.Reason ?? string.Empty).Trim();
        if (reason.Length == 0 || reason.Length > MaxReasonLength)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Reason is required");

        if (request.StartDate > request.EndDate)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Start date is after end date");

        Guid? venueId = null;
        Guid? teamId = null;

        switch (scope)
        {
            case BlockedDateScope.Venue:
                if (request.VenueId is not { } vId)
                    return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "venueId is required for a Venue block");
                var venue = await venues.GetByIdAsync(vId, ct);
                if (venue is null || venue.ClubId != clubId) return Results.NotFound();
                venueId = vId;
                break;

            case BlockedDateScope.Team:
                if (request.TeamId is not { } tId)
                    return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "teamId is required for a Team block");
                var team = await teams.GetByIdAsync(tId, ct);
                if (team is null || team.ClubId != clubId) return Results.NotFound();
                teamId = tId;
                break;

            case BlockedDateScope.Club:
            default:
                break;
        }

        var id = await blockedDates.CreateAsync(
            clubId, scope, venueId, teamId, request.StartDate, request.EndDate, reason, ct);

        return Results.Created(
            $"/api/clubs/{clubId}/blocked-dates/{id}",
            new BlockedDateResponse(id, clubId, scope.ToString(), venueId, teamId, request.StartDate, request.EndDate, reason));
    }
}
