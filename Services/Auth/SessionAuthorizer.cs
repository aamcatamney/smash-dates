using System.Security.Claims;
using smash_dates.Repositories;

namespace smash_dates.Services.Auth;

/// A request may run a club's pegboard session if the caller is SystemAdmin, a ClubAdmin
/// of the club, or a SessionHost of the club. Returns null on success, else a 401/403 IResult.
public static class SessionAuthorizer
{
    public static async Task<IResult?> RequireSessionRunnerAsync(
        ClaimsPrincipal principal,
        Guid clubId,
        IClubAdminRepository admins,
        ISessionHostRepository hosts,
        CancellationToken ct)
    {
        var userId = principal.UserId();
        if (userId is null) return Results.Unauthorized();
        if (principal.IsSystemAdmin()) return null;
        if (await admins.IsAdminAsync(clubId, userId.Value, ct)) return null;
        if (await hosts.IsHostAsync(clubId, userId.Value, ct)) return null;
        return Results.Forbid();
    }
}
