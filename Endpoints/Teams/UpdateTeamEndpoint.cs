using System.Security.Claims;
using Npgsql;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Teams;

public static class UpdateTeamEndpoint
{
    private const int MaxNameLength = 200;
    private const string DuplicateSqlState = "23505";

    // Name only — gender is immutable after creation (see CONTEXT.md).
    public sealed record UpdateTeamRequest(string Name);

    public static IEndpointRouteBuilder MapUpdateTeamEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/{id:guid}", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId,
        Guid id,
        UpdateTeamRequest request,
        ClaimsPrincipal principal,
        ITeamRepository teams,
        IClubAdminRepository clubAdmins,
        CancellationToken ct)
    {
        var team = await teams.GetByIdAsync(id, ct);
        if (team is null || team.ClubId != clubId) return Results.NotFound();

        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, clubId, clubAdmins, ct);
        if (authz is not null) return authz;

        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0 || name.Length > MaxNameLength)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid name");

        try
        {
            await teams.UpdateNameAsync(id, name, ct);
            return Results.NoContent();
        }
        catch (PostgresException ex) when (ex.SqlState == DuplicateSqlState)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Team name already in use in this club");
        }
    }
}
