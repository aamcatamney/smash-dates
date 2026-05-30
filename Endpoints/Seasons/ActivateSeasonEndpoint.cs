using System.Security.Claims;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Seasons;

public static class ActivateSeasonEndpoint
{
    public static IEndpointRouteBuilder MapActivateSeasonEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{id:guid}/activate", Handle);
        return app;
    }

    // Manual Proposed → Active. (Auto-activation on the first match date is deferred — it
    // needs a scheduled job, the same family as the deferred async scheduler runner.)
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

        if (!await seasons.TransitionStatusAsync(id, SeasonStatus.Proposed, SeasonStatus.Active, ct))
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Only a Proposed season can be activated");

        return Results.Ok(new { status = SeasonStatus.Active.ToString() });
    }
}
