using System.Security.Claims;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;
using smash_dates.Services.Notifications;

namespace smash_dates.Endpoints.Matches;

public static class ForceConfirmMatchEndpoint
{
    public sealed record ForceConfirmResult(string Status);

    public static IEndpointRouteBuilder MapForceConfirmMatchEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{id:guid}/force-confirm", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        ClaimsPrincipal principal,
        IMatchRepository matches,
        ISeasonRepository seasons,
        ILeagueAdminRepository leagueAdmins,
        INotificationService notifications,
        CancellationToken ct)
    {
        var match = await matches.GetByIdAsync(id, ct);
        if (match is null) return Results.NotFound();

        var season = await seasons.GetByIdAsync(match.SeasonId, ct);
        if (season is null) return Results.NotFound();

        var authz = await LeagueAuthorizer.RequireLeagueAdminAsync(principal, season.LeagueId, leagueAdmins, ct);
        if (authz is not null) return authz;

        if (match.Status != MatchStatus.Proposed)
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Only a Proposed match can be force-confirmed");

        if (!await matches.ForceConfirmAsync(id, ct))
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Only a Proposed match can be force-confirmed");

        await notifications.MatchStatusChangedAsync(id, MatchStatus.Confirmed, ct);
        return Results.Ok(new ForceConfirmResult(MatchStatus.Confirmed.ToString()));
    }
}
