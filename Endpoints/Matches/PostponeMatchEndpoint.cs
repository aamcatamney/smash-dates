using System.Security.Claims;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Matches;

public static class PostponeMatchEndpoint
{
    public static IEndpointRouteBuilder MapPostponeMatchEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{id:guid}/postpone", Handle);
        return app;
    }

    // LeagueAdmin-executed postpone: a Confirmed match in an Active season returns to
    // Proposed (acceptance cleared) so /rerun can re-place it. The clubs' agreement is
    // confirmed by the LeagueAdmin out-of-band.
    private static async Task<IResult> Handle(
        Guid id,
        ClaimsPrincipal principal,
        IMatchRepository matches,
        ISeasonRepository seasons,
        ILeagueAdminRepository leagueAdmins,
        CancellationToken ct)
    {
        var match = await matches.GetByIdAsync(id, ct);
        if (match is null) return Results.NotFound();

        var season = await seasons.GetByIdAsync(match.SeasonId, ct);
        if (season is null) return Results.NotFound();

        var authz = await LeagueAuthorizer.RequireLeagueAdminAsync(principal, season.LeagueId, leagueAdmins, ct);
        if (authz is not null) return authz;

        if (season.Status != SeasonStatus.Active)
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Matches can only be postponed once the season is Active");

        if (match.Status != MatchStatus.Confirmed)
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Only a Confirmed match can be postponed");

        if (!await matches.PostponeAsync(id, ct))
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Only a Confirmed match can be postponed");

        return Results.Ok(new { status = MatchStatus.Proposed.ToString() });
    }
}
