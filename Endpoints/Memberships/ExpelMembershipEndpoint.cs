using System.Security.Claims;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Memberships;

public static class ExpelMembershipEndpoint
{
    public static IEndpointRouteBuilder MapExpelMembershipEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{membershipId:guid}/expel", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid leagueId,
        Guid membershipId,
        ClaimsPrincipal principal,
        ILeagueRepository leagues,
        ILeagueAdminRepository leagueAdmins,
        IClubLeagueMembershipRepository memberships,
        ISeasonEntryRepository seasonEntries,
        CancellationToken ct)
    {
        if (await leagues.GetByIdAsync(leagueId, ct) is null) return Results.NotFound();

        var membership = await memberships.GetByIdAsync(membershipId, ct);
        if (membership is null || membership.LeagueId != leagueId) return Results.NotFound();

        var authz = await LeagueAuthorizer.RequireLeagueAdminAsync(principal, leagueId, leagueAdmins, ct);
        if (authz is not null) return authz;

        // Mid-season block (CONTEXT.md): can't expel while the club has a team entered in
        // a non-Closed season of this league.
        if (await seasonEntries.ClubHasOpenSeasonEntryInLeagueAsync(membership.ClubId, leagueId, ct))
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Can't expel mid-season: the club has a team in an open season");

        var userId = principal.UserId()!.Value;
        var ok = await memberships.TransitionFromAcceptedAsync(membershipId, MembershipStatus.Expelled, userId, ct);
        return ok
            ? Results.NoContent()
            : Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Membership is not Accepted");
    }
}
