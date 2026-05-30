using System.Security.Claims;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Matches;

public static class MatchActionEndpoints
{
    public static IEndpointRouteBuilder MapMatchActionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/matches").RequireAuthorization();

        group.MapGetMatchEndpoint();
        group.MapAcceptMatchEndpoint();
        group.MapRejectMatchEndpoint();
        group.MapForceConfirmMatchEndpoint();
        group.MapRecordResultEndpoint();
        group.MapRecordWalkoverEndpoint();
        return app;
    }

    // Which sides of the match the caller may act for. SystemAdmin may act for both.
    internal static async Task<(bool CanHome, bool CanAway)> ResolveSidesAsync(
        ClaimsPrincipal principal,
        Match match,
        ITeamRepository teams,
        IClubAdminRepository clubAdmins,
        CancellationToken ct)
    {
        if (principal.IsSystemAdmin()) return (true, true);

        var userId = principal.UserId();
        if (userId is null) return (false, false);

        var home = await teams.GetByIdAsync(match.HomeTeamId, ct);
        var away = await teams.GetByIdAsync(match.AwayTeamId, ct);

        var canHome = home is not null && await clubAdmins.IsAdminAsync(home.ClubId, userId.Value, ct);
        var canAway = away is not null && await clubAdmins.IsAdminAsync(away.ClubId, userId.Value, ct);
        return (canHome, canAway);
    }
}
