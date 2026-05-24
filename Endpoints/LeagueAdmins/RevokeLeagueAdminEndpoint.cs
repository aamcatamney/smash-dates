using System.Security.Claims;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.LeagueAdmins;

public static class RevokeLeagueAdminEndpoint
{
    public static IEndpointRouteBuilder MapRevokeLeagueAdminEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/{userId:guid}", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid leagueId,
        Guid userId,
        ClaimsPrincipal principal,
        ILeagueRepository leagues,
        ILeagueAdminRepository admins,
        CancellationToken ct)
    {
        if (await leagues.GetByIdAsync(leagueId, ct) is null)
            return Results.NotFound();

        var authz = await LeagueAuthorizer.RequireLeagueAdminAsync(principal, leagueId, admins, ct);
        if (authz is not null) return authz;

        // SystemAdmin can force-revoke even when it would leave the league adminless.
        if (principal.IsSystemAdmin())
        {
            var removed = await admins.RevokeAsync(leagueId, userId, ct);
            return removed ? Results.NoContent() : Results.NotFound();
        }

        var outcome = await admins.RevokeUnlessLastAsync(leagueId, userId, ct);
        return outcome switch
        {
            RevokeResult.Revoked => Results.NoContent(),
            RevokeResult.NotAdmin => Results.NotFound(),
            RevokeResult.WouldBeLastAdmin => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Cannot remove the last LeagueAdmin",
                detail: "Grant LeagueAdmin to another user first, or ask a SystemAdmin to force the removal."),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }
}
