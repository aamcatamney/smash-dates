using System.Security.Claims;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.BlockedDates;

public static class DeleteBlockedDateEndpoint
{
    public static IEndpointRouteBuilder MapDeleteBlockedDateEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/{id:guid}", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId,
        Guid id,
        ClaimsPrincipal principal,
        IBlockedDateRepository blockedDates,
        IClubAdminRepository clubAdmins,
        ISeasonEntryRepository seasonEntries,
        CancellationToken ct)
    {
        var block = await blockedDates.GetByIdAsync(id, ct);
        if (block is null || block.ClubId != clubId) return Results.NotFound();

        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, clubId, clubAdmins, ct);
        if (authz is not null) return authz;

        if (await seasonEntries.ClubHasActiveSeasonEntryAsync(clubId, ct))
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Blocked dates can't be changed while the club is in an active season");

        await blockedDates.DeleteAsync(id, ct);
        return Results.NoContent();
    }
}
