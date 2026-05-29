using System.Security.Claims;
using Npgsql;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Venues;

public static class UpdateVenueEndpoint
{
    private const int MaxNameLength = 200;
    private const string DuplicateSqlState = "23505";

    public sealed record UpdateVenueRequest(string Name, int Capacity);

    public static IEndpointRouteBuilder MapUpdateVenueEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/{id:guid}", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId,
        Guid id,
        UpdateVenueRequest request,
        ClaimsPrincipal principal,
        IVenueRepository venues,
        IClubAdminRepository clubAdmins,
        CancellationToken ct)
    {
        var venue = await venues.GetByIdAsync(id, ct);
        if (venue is null || venue.ClubId != clubId) return Results.NotFound();

        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, clubId, clubAdmins, ct);
        if (authz is not null) return authz;

        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0 || name.Length > MaxNameLength)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid name");

        if (request.Capacity is not (1 or 2))
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Capacity must be 1 or 2");

        try
        {
            await venues.UpdateAsync(id, name, request.Capacity, ct);
            return Results.NoContent();
        }
        catch (PostgresException ex) when (ex.SqlState == DuplicateSqlState)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Venue name already in use in this club");
        }
    }
}
