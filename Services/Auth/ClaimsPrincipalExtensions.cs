using System.Security.Claims;

namespace smash_dates.Services.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid? UserId(this ClaimsPrincipal principal)
    {
        var idClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(idClaim, out var id) ? id : null;
    }

    public static bool IsSystemAdmin(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(AuthorizationPolicies.SystemAdminClaim) == "true";
    }
}
