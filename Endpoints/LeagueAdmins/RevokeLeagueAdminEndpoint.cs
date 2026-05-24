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

        var isAdmin = await admins.IsAdminAsync(leagueId, userId, ct);
        if (!isAdmin) return Results.NotFound();

        if (!principal.IsSystemAdmin())
        {
            var count = await admins.CountByLeagueAsync(leagueId, ct);
            if (count <= 1)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Cannot remove the last LeagueAdmin",
                    detail: "Grant LeagueAdmin to another user first, or ask a SystemAdmin to force the removal.");
            }
        }

        await admins.RevokeAsync(leagueId, userId, ct);
        return Results.NoContent();
    }
}
