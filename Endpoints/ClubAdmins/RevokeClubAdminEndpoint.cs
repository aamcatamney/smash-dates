using System.Security.Claims;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.ClubAdmins;

public static class RevokeClubAdminEndpoint
{
    public static IEndpointRouteBuilder MapRevokeClubAdminEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/{userId:guid}", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId,
        Guid userId,
        ClaimsPrincipal principal,
        IClubRepository clubs,
        IClubAdminRepository admins,
        CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(clubId, ct) is null) return Results.NotFound();

        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, clubId, admins, ct);
        if (authz is not null) return authz;

        if (principal.IsSystemAdmin())
        {
            var removed = await admins.RevokeAsync(clubId, userId, ct);
            return removed ? Results.NoContent() : Results.NotFound();
        }

        var outcome = await admins.RevokeUnlessLastAsync(clubId, userId, ct);
        return outcome switch
        {
            RevokeResult.Revoked => Results.NoContent(),
            RevokeResult.NotAdmin => Results.NotFound(),
            RevokeResult.WouldBeLastAdmin => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Cannot remove the last ClubAdmin",
                detail: "Grant ClubAdmin to another user first, or ask a SystemAdmin to force the removal."),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }
}
