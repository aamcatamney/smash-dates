using System.Security.Claims;
using Npgsql;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.LeagueAdmins;

public static class GrantLeagueAdminEndpoint
{
    private const string ForeignKeyViolationSqlState = "23503";

    public sealed record GrantRequest(Guid UserId);

    public static IEndpointRouteBuilder MapGrantLeagueAdminEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid leagueId,
        GrantRequest request,
        ClaimsPrincipal principal,
        ILeagueRepository leagues,
        ILeagueAdminRepository admins,
        IUserRepository users,
        CancellationToken ct)
    {
        if (await leagues.GetByIdAsync(leagueId, ct) is null)
            return Results.NotFound();

        var authz = await LeagueAuthorizer.RequireLeagueAdminAsync(principal, leagueId, admins, ct);
        if (authz is not null) return authz;

        if (request.UserId == Guid.Empty)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "userId is required");

        if (await users.GetByIdAsync(request.UserId, ct) is null)
            return Results.NotFound();

        var grantedBy = principal.UserId()!.Value;

        try
        {
            await admins.GrantAsync(leagueId, request.UserId, grantedBy, ct);
        }
        catch (PostgresException ex) when (ex.SqlState == ForeignKeyViolationSqlState)
        {
            return Results.NotFound();
        }

        return Results.NoContent();
    }
}
