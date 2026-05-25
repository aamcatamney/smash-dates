using System.Security.Claims;
using smash_dates.Repositories;

namespace smash_dates.Services.Auth;

/// Inline authorization helper: a request is permitted if the caller is SystemAdmin
/// or holds a LeagueAdmin grant for the specific league referenced in the route.
/// Returns null on success, or a 401/403 IResult to short-circuit the endpoint.
public static class LeagueAuthorizer
{
    public static async Task<IResult?> RequireLeagueAdminAsync(
        ClaimsPrincipal principal,
        Guid leagueId,
        ILeagueAdminRepository admins,
        CancellationToken ct)
    {
        var userId = principal.UserId();
        if (userId is null) return Results.Unauthorized();

        if (principal.IsSystemAdmin()) return null;

        var isAdmin = await admins.IsAdminAsync(leagueId, userId.Value, ct);
        return isAdmin ? null : Results.Forbid();
    }
}
