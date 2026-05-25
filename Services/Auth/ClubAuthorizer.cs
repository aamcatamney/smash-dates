using System.Security.Claims;
using smash_dates.Repositories;

namespace smash_dates.Services.Auth;

/// Inline authorisation helper: a request is permitted if the caller is SystemAdmin
/// or holds a ClubAdmin grant for the specific club. Returns null on success or a
/// 401/403 IResult to short-circuit the endpoint.
public static class ClubAuthorizer
{
    public static async Task<IResult?> RequireClubAdminAsync(
        ClaimsPrincipal principal,
        Guid clubId,
        IClubAdminRepository admins,
        CancellationToken ct)
    {
        var userId = principal.UserId();
        if (userId is null) return Results.Unauthorized();

        if (principal.IsSystemAdmin()) return null;

        var isAdmin = await admins.IsAdminAsync(clubId, userId.Value, ct);
        return isAdmin ? null : Results.Forbid();
    }
}
