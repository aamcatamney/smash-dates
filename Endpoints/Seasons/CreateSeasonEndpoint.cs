using System.Security.Claims;
using Npgsql;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Seasons;

public static class CreateSeasonEndpoint
{
    private const int MaxNameLength = 100;
    private const string DuplicateSqlState = "23505";

    public sealed record CreateSeasonRequest(
        string Name,
        DateOnly StartDate,
        DateOnly EndDate,
        WeekInput[]? Weeks);

    public sealed record SeasonResponse(
        Guid Id,
        Guid LeagueId,
        string Name,
        DateOnly StartDate,
        DateOnly EndDate,
        string Status);

    public static IEndpointRouteBuilder MapCreateSeasonEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid leagueId,
        CreateSeasonRequest request,
        ClaimsPrincipal principal,
        ILeagueRepository leagues,
        ISeasonRepository seasons,
        ILeagueAdminRepository leagueAdmins,
        CancellationToken ct)
    {
        if (await leagues.GetByIdAsync(leagueId, ct) is null) return Results.NotFound();

        var authz = await LeagueAuthorizer.RequireLeagueAdminAsync(principal, leagueId, leagueAdmins, ct);
        if (authz is not null) return authz;

        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0 || name.Length > MaxNameLength)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid name");

        if (request.StartDate > request.EndDate)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Season start date is after its end date");

        var weeksError = SeasonWeekValidation.Validate(
            request.StartDate, request.EndDate, request.Weeks ?? [], out var weeks);
        if (weeksError is not null)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: weeksError);

        try
        {
            var id = await seasons.CreateWithWeeksAsync(
                leagueId, name, request.StartDate, request.EndDate,
                weeks.Select(w => (w.StartDate, w.EndDate, w.WeekType)).ToList(), ct);

            return Results.Created($"/api/leagues/{leagueId}/seasons/{id}",
                new SeasonResponse(id, leagueId, name, request.StartDate, request.EndDate, "Draft"));
        }
        catch (PostgresException ex) when (ex.SqlState == DuplicateSqlState)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "A season with that name already exists in this league");
        }
    }
}
