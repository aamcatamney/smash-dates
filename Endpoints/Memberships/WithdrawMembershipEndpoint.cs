using System.Security.Claims;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Memberships;

public static class WithdrawMembershipEndpoint
{
    public static IEndpointRouteBuilder MapWithdrawMembershipEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{membershipId:guid}/withdraw", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid leagueId,
        Guid membershipId,
        ClaimsPrincipal principal,
        ILeagueRepository leagues,
        IClubLeagueMembershipRepository memberships,
        IClubAdminRepository clubAdmins,
        CancellationToken ct)
    {
        if (await leagues.GetByIdAsync(leagueId, ct) is null) return Results.NotFound();

        var membership = await memberships.GetByIdAsync(membershipId, ct);
        if (membership is null || membership.LeagueId != leagueId) return Results.NotFound();

        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, membership.ClubId, clubAdmins, ct);
        if (authz is not null) return authz;

        // Mid-season block deferred to the slice that adds Season Entries.
        var userId = principal.UserId()!.Value;
        var ok = await memberships.TransitionFromAcceptedAsync(membershipId, MembershipStatus.Withdrawn, userId, ct);
        return ok
            ? Results.NoContent()
            : Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Membership is not Accepted");
    }
}
