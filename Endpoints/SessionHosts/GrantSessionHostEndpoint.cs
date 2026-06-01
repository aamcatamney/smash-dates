using System.Security.Claims;
using Npgsql;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.SessionHosts;

public static class GrantSessionHostEndpoint
{
    public sealed record GrantRequest(Guid UserId);

    public static IEndpointRouteBuilder MapGrantSessionHostEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId, GrantRequest request, ClaimsPrincipal principal,
        IClubRepository clubs, IClubAdminRepository admins, ISessionHostRepository hosts, CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(clubId, ct) is null) return Results.NotFound();
        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, clubId, admins, ct);
        if (authz is not null) return authz;
        if (request.UserId == Guid.Empty)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "UserId is required");

        try
        {
            await hosts.GrantAsync(clubId, request.UserId, principal.UserId(), ct);
            return Results.NoContent();
        }
        catch (PostgresException ex) when (ex.SqlState == "23503")
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "UserId references unknown user");
        }
    }
}
