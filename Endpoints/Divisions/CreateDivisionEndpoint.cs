using Npgsql;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Divisions;

public static class CreateDivisionEndpoint
{
    private const int MaxNameLength = 200;
    private const string DuplicateNameSqlState = "23505";

    public sealed record CreateDivisionRequest(
        string Name,
        string Gender,
        int Rank,
        int RubbersPerMatch,
        int WinPoints,
        int DrawPoints,
        int LossPoints);

    public sealed record DivisionResponse(
        Guid Id,
        Guid LeagueId,
        string Name,
        string Gender,
        int Rank,
        int RubbersPerMatch,
        int WinPoints,
        int DrawPoints,
        int LossPoints);

    public static IEndpointRouteBuilder MapCreateDivisionEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("", Handle)
            .RequireAuthorization(AuthorizationPolicies.SystemAdmin);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid leagueId,
        CreateDivisionRequest request,
        ILeagueRepository leagues,
        IDivisionRepository divisions,
        CancellationToken ct)
    {
        var league = await leagues.GetByIdAsync(leagueId, ct);
        if (league is null) return Results.NotFound();

        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0 || name.Length > MaxNameLength)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid name");

        if (!Enum.TryParse<DivisionGender>(request.Gender, ignoreCase: false, out var gender))
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid gender");

        if (request.Rank < 1)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Rank must be 1 or more");

        if (request.RubbersPerMatch <= 0)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "RubbersPerMatch must be positive");

        if (request.WinPoints < 0 || request.DrawPoints < 0 || request.LossPoints < 0)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Points must be non-negative");

        try
        {
            var id = await divisions.CreateAsync(
                leagueId, name, gender, request.Rank, request.RubbersPerMatch,
                request.WinPoints, request.DrawPoints, request.LossPoints, ct);

            return Results.Created($"/api/leagues/{leagueId}/divisions/{id}", new DivisionResponse(
                id, leagueId, name, gender.ToString(), request.Rank, request.RubbersPerMatch,
                request.WinPoints, request.DrawPoints, request.LossPoints));
        }
        catch (PostgresException ex) when (ex.SqlState == DuplicateNameSqlState)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Division already exists");
        }
    }
}
