using System.Security.Claims;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Seasons;

public static class ReplaceSeasonWeeksEndpoint
{
    public sealed record ReplaceWeeksRequest(WeekInput[]? Weeks);

    public static IEndpointRouteBuilder MapReplaceSeasonWeeksEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPut("/{id:guid}/weeks", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid leagueId,
        Guid id,
        ReplaceWeeksRequest request,
        ClaimsPrincipal principal,
        ISeasonRepository seasons,
        ILeagueAdminRepository leagueAdmins,
        CancellationToken ct)
    {
        var season = await seasons.GetByIdAsync(id, ct);
        if (season is null || season.LeagueId != leagueId) return Results.NotFound();

        var authz = await LeagueAuthorizer.RequireLeagueAdminAsync(principal, leagueId, leagueAdmins, ct);
        if (authz is not null) return authz;

        if (season.Status != SeasonStatus.Draft)
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Weeks can only be edited while the season is Draft");

        var error = SeasonWeekValidation.Validate(
            season.StartDate, season.EndDate, request.Weeks ?? [], out var weeks);
        if (error is not null)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: error);

        await seasons.ReplaceWeeksAsync(
            id, weeks.Select(w => (w.StartDate, w.EndDate, w.WeekType)).ToList(), ct);
        return Results.NoContent();
    }
}
