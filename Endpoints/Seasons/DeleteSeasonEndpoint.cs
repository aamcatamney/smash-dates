using System.Security.Claims;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Seasons;

public static class DeleteSeasonEndpoint
{
    public static IEndpointRouteBuilder MapDeleteSeasonEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/{id:guid}", Handle);
        return app;
    }

    // Deletable only while Draft; weeks cascade via FK.
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

        if (season.Status != SeasonStatus.Draft)
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Only a Draft season can be deleted");

        await seasons.DeleteAsync(id, ct);
        return Results.NoContent();
    }
}
