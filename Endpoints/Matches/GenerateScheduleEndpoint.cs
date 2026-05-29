using System.Security.Claims;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;
using smash_dates.Services.Scheduling;

namespace smash_dates.Endpoints.Matches;

public static class GenerateScheduleEndpoint
{
    public static IEndpointRouteBuilder MapGenerateScheduleEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/generate", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid leagueId,
        Guid seasonId,
        ClaimsPrincipal principal,
        ISeasonRepository seasons,
        ILeagueAdminRepository leagueAdmins,
        IScheduleGenerator generator,
        CancellationToken ct)
    {
        var season = await seasons.GetByIdAsync(seasonId, ct);
        if (season is null || season.LeagueId != leagueId) return Results.NotFound();

        var authz = await LeagueAuthorizer.RequireLeagueAdminAsync(principal, leagueId, leagueAdmins, ct);
        if (authz is not null) return authz;

        if (season.Status != SeasonStatus.Draft)
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Schedule can only be generated from a Draft season");

        var result = await generator.GenerateAsync(seasonId, ct);

        if (!result.Success)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Could not place all matches under the hard constraints",
                extensions: new Dictionary<string, object?>
                {
                    ["unplaced"] = result.Unplaced
                        .Select(u => new { u.DivisionId, u.HomeTeamId, u.AwayTeamId })
                        .ToArray(),
                });
        }

        return Results.Ok(new { matchCount = result.Matches.Count });
    }
}
