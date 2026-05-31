using System.Security.Claims;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.SessionHosts;

public static class RevokeSessionHostEndpoint
{
    public static IEndpointRouteBuilder MapRevokeSessionHostEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/{userId:guid}", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId, Guid userId, ClaimsPrincipal principal,
        IClubRepository clubs, IClubAdminRepository admins, ISessionHostRepository hosts, CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(clubId, ct) is null) return Results.NotFound();
        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, clubId, admins, ct);
        if (authz is not null) return authz;
        // No last-host protection (CONTEXT.md): a club may have zero hosts.
        return await hosts.RevokeAsync(clubId, userId, ct) ? Results.NoContent() : Results.NotFound();
    }
}
