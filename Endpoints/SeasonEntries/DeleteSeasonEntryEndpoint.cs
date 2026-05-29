using System.Security.Claims;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.SeasonEntries;

public static class DeleteSeasonEntryEndpoint
{
    public static IEndpointRouteBuilder MapDeleteSeasonEntryEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/{id:guid}", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid leagueId,
        Guid seasonId,
        Guid id,
        ClaimsPrincipal principal,
        ISeasonRepository seasons,
        ISeasonEntryRepository entries,
        ILeagueAdminRepository leagueAdmins,
        CancellationToken ct)
    {
        var entry = await entries.GetByIdAsync(id, ct);
        if (entry is null || entry.SeasonId != seasonId) return Results.NotFound();

        var season = await seasons.GetByIdAsync(seasonId, ct);
        if (season is null || season.LeagueId != leagueId) return Results.NotFound();

        var authz = await LeagueAuthorizer.RequireLeagueAdminAsync(principal, leagueId, leagueAdmins, ct);
        if (authz is not null) return authz;

        if (season.Status != SeasonStatus.Draft)
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Entries can only be removed while the season is Draft");

        await entries.DeleteAsync(id, ct);
        return Results.NoContent();
    }
}
