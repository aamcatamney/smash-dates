using System.Security.Claims;
using Npgsql;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Teams;

public static class CreateTeamEndpoint
{
    private const int MaxNameLength = 200;
    private const string DuplicateSqlState = "23505";

    public sealed record CreateTeamRequest(string Name, string Gender);

    public sealed record TeamResponse(Guid Id, Guid ClubId, string Name, string Gender);

    public static IEndpointRouteBuilder MapCreateTeamEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId,
        CreateTeamRequest request,
        ClaimsPrincipal principal,
        IClubRepository clubs,
        ITeamRepository teams,
        IClubAdminRepository clubAdmins,
        CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(clubId, ct) is null) return Results.NotFound();

        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, clubId, clubAdmins, ct);
        if (authz is not null) return authz;

        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0 || name.Length > MaxNameLength)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid name");

        if (!Enum.TryParse<DivisionGender>(request.Gender, ignoreCase: false, out var gender))
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid gender");

        try
        {
            var id = await teams.CreateAsync(clubId, name, gender, ct);
            return Results.Created($"/api/clubs/{clubId}/teams", new TeamResponse(id, clubId, name, gender.ToString()));
        }
        catch (PostgresException ex) when (ex.SqlState == DuplicateSqlState)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Team name already in use in this club");
        }
    }
}
