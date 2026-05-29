using System.Security.Claims;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Teams;

public static class DeleteTeamEndpoint
{
    public static IEndpointRouteBuilder MapDeleteTeamEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/{id:guid}", Handle);
        return app;
    }

    // Hard delete, guarded by existence + club ownership, and rejected (409) once the
    // Team is referenced by a Season Entry (see CONTEXT.md guarded delete).
    private static async Task<IResult> Handle(
        Guid clubId,
        Guid id,
        ClaimsPrincipal principal,
        ITeamRepository teams,
        IClubAdminRepository clubAdmins,
        ISeasonEntryRepository seasonEntries,
        CancellationToken ct)
    {
        var team = await teams.GetByIdAsync(id, ct);
        if (team is null || team.ClubId != clubId) return Results.NotFound();

        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, clubId, clubAdmins, ct);
        if (authz is not null) return authz;

        if (await seasonEntries.ExistsForTeamAsync(id, ct))
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Team is assigned to a season and cannot be deleted");

        await teams.DeleteAsync(id, ct);
        return Results.NoContent();
    }
}
