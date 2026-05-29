using System.Security.Claims;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Venues;

public static class DeleteVenueEndpoint
{
    public static IEndpointRouteBuilder MapDeleteVenueEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/{id:guid}", Handle);
        return app;
    }

    // Hard delete, guarded by existence + club ownership, and rejected (409) once the
    // Venue is referenced by a scheduled Match (see CONTEXT.md guarded delete).
    private static async Task<IResult> Handle(
        Guid clubId,
        Guid id,
        ClaimsPrincipal principal,
        IVenueRepository venues,
        IClubAdminRepository clubAdmins,
        IMatchRepository matches,
        CancellationToken ct)
    {
        var venue = await venues.GetByIdAsync(id, ct);
        if (venue is null || venue.ClubId != clubId) return Results.NotFound();

        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, clubId, clubAdmins, ct);
        if (authz is not null) return authz;

        if (await matches.ExistsForVenueAsync(id, ct))
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Venue is used by a scheduled match and cannot be deleted");

        await venues.DeleteAsync(id, ct);
        return Results.NoContent();
    }
}
