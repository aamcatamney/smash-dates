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

    // Generation can be slow (double round-robin + 2-opt), so it runs off the request thread:
    // this moves the season to Scheduling and returns 202; ScheduleRunner (a background hosted
    // service) does the work and moves it to Proposed, or back to Draft with a scheduling error.
    private static async Task<IResult> Handle(
        Guid leagueId,
        Guid seasonId,
        ClaimsPrincipal principal,
        ISeasonRepository seasons,
        ILeagueAdminRepository leagueAdmins,
        CancellationToken ct)
    {
        var season = await seasons.GetByIdAsync(seasonId, ct);
        if (season is null || season.LeagueId != leagueId) return Results.NotFound();

        var authz = await LeagueAuthorizer.RequireLeagueAdminAsync(principal, leagueId, leagueAdmins, ct);
        if (authz is not null) return authz;

        if (!await seasons.BeginSchedulingAsync(seasonId, ct))
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Schedule can only be generated from a Draft season");

        return Results.Accepted();
    }
}
