using System.Security.Claims;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;
using smash_dates.Services.Notifications;

namespace smash_dates.Endpoints.Memberships;

public static class DeclineMembershipEndpoint
{
    public static IEndpointRouteBuilder MapDeclineMembershipEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{membershipId:guid}/decline", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid leagueId,
        Guid membershipId,
        ClaimsPrincipal principal,
        ILeagueRepository leagues,
        IClubLeagueMembershipRepository memberships,
        IClubAdminRepository clubAdmins,
        INotificationService notifications,
        CancellationToken ct)
    {
        if (await leagues.GetByIdAsync(leagueId, ct) is null) return Results.NotFound();

        var membership = await memberships.GetByIdAsync(membershipId, ct);
        if (membership is null || membership.LeagueId != leagueId) return Results.NotFound();

        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, membership.ClubId, clubAdmins, ct);
        if (authz is not null) return authz;

        var userId = principal.UserId()!.Value;
        var ok = await memberships.TransitionFromPendingAsync(membershipId, MembershipStatus.Declined, userId, ct);
        if (!ok) return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Membership is not Pending");

        await notifications.MembershipRespondedAsync(membership.ClubId, leagueId, MembershipStatus.Declined, ct);
        return Results.NoContent();
    }
}
