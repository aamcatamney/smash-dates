using System.Security.Claims;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Seasons;

public static class CloseSeasonEndpoint
{
    public static IEndpointRouteBuilder MapCloseSeasonEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{id:guid}/close", Handle);
        return app;
    }

    // Manual Active → Closed. (Auto-close on the season end date is deferred — needs a job.)
    private static async Task<IResult> Handle(
        Guid leagueId,
        Guid id,
        ClaimsPrincipal principal,
        ISeasonRepository seasons,
        ILeagueAdminRepository leagueAdmins,
        CancellationToken ct)
    {
        var season = await seasons.GetByIdAsync(id, ct);
        if (season is null || season.LeagueId != leagueId) return Results.NotFound();

        var authz = await LeagueAuthorizer.RequireLeagueAdminAsync(principal, leagueId, leagueAdmins, ct);
        if (authz is not null) return authz;

        if (!await seasons.TransitionStatusAsync(id, SeasonStatus.Active, SeasonStatus.Closed, ct))
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Only an Active season can be closed");

        return Results.Ok(new { status = SeasonStatus.Closed.ToString() });
    }
}
