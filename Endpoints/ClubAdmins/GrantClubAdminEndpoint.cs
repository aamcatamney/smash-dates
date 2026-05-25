using System.Security.Claims;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.ClubAdmins;

public static class GrantClubAdminEndpoint
{
    public sealed record GrantRequest(Guid UserId);

    public static IEndpointRouteBuilder MapGrantClubAdminEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId,
        GrantRequest request,
        ClaimsPrincipal principal,
        IClubRepository clubs,
        IClubAdminRepository admins,
        IUserRepository users,
        CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(clubId, ct) is null) return Results.NotFound();

        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, clubId, admins, ct);
        if (authz is not null) return authz;

        if (request.UserId == Guid.Empty)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "userId is required");

        if (await users.GetByIdAsync(request.UserId, ct) is null)
            return Results.NotFound();

        var grantedBy = principal.UserId()!.Value;
        await admins.GrantAsync(clubId, request.UserId, grantedBy, ct);
        return Results.NoContent();
    }
}
