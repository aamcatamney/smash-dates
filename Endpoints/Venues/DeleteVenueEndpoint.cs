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

    // Hard delete, guarded by existence + club ownership. Referential guards (409 once a
    // Venue has hosted a Match) will be added when the matches table lands.
    private static async Task<IResult> Handle(
        Guid clubId,
        Guid id,
        ClaimsPrincipal principal,
        IVenueRepository venues,
        IClubAdminRepository clubAdmins,
        CancellationToken ct)
    {
        var venue = await venues.GetByIdAsync(id, ct);
        if (venue is null || venue.ClubId != clubId) return Results.NotFound();

        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, clubId, clubAdmins, ct);
        if (authz is not null) return authz;

        await venues.DeleteAsync(id, ct);
        return Results.NoContent();
    }
}
