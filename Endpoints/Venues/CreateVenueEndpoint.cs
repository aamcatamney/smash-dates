using System.Security.Claims;
using Npgsql;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Venues;

public static class CreateVenueEndpoint
{
    private const int MaxNameLength = 200;
    private const string DuplicateSqlState = "23505";

    public sealed record CreateVenueRequest(string Name, int? Courts, int? MaxConcurrentMatches);

    public sealed record VenueResponse(Guid Id, Guid ClubId, string Name, int Courts, int MaxConcurrentMatches);

    public static IEndpointRouteBuilder MapCreateVenueEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId,
        CreateVenueRequest request,
        ClaimsPrincipal principal,
        IClubRepository clubs,
        IVenueRepository venues,
        IClubAdminRepository clubAdmins,
        CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(clubId, ct) is null) return Results.NotFound();

        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, clubId, clubAdmins, ct);
        if (authz is not null) return authz;

        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0 || name.Length > MaxNameLength)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid name");

        var courts = request.Courts ?? 2;
        if (courts < 1)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Courts must be at least 1");

        var maxConcurrent = request.MaxConcurrentMatches ?? 1;
        if (maxConcurrent is not (1 or 2))
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Max concurrent matches must be 1 or 2");

        try
        {
            var id = await venues.CreateAsync(clubId, name, courts, maxConcurrent, ct);
            return Results.Created($"/api/clubs/{clubId}/venues", new VenueResponse(id, clubId, name, courts, maxConcurrent));
        }
        catch (PostgresException ex) when (ex.SqlState == DuplicateSqlState)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Venue name already in use in this club");
        }
    }
}
