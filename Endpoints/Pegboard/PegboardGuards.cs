using System.Security.Claims;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Pegboard;

public static class PegboardGuards
{
    // Returns (session, null) when the caller may mutate an Open session of this club,
    // else (null, errorResult). 404 if missing/club-mismatch, 409 if closed, 401/403 if not allowed.
    public static async Task<(PegboardSession? Session, IResult? Error)> LoadOpenForMutationAsync(
        Guid clubId, Guid sessionId, ClaimsPrincipal principal,
        IPegboardRepository pegboard, IClubAdminRepository admins, ISessionHostRepository hosts, CancellationToken ct)
    {
        var session = await pegboard.GetSessionAsync(sessionId, ct);
        if (session is null || session.ClubId != clubId) return (null, Results.NotFound());

        var authz = await SessionAuthorizer.RequireSessionRunnerAsync(principal, clubId, admins, hosts, ct);
        if (authz is not null) return (null, authz);

        if (session.Status != PegboardSessionStatus.Open)
            return (null, Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Session is not open"));

        return (session, null);
    }
}
