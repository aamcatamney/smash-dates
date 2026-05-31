using System.Security.Claims;
using Npgsql;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Pegboard;

public static class OpenSessionEndpoint
{
    private const int MaxNameLength = 200;
    public sealed record OpenRequest(string Name);
    public sealed record SessionDto(Guid Id, Guid ClubId, string Name, string Status);

    public static IEndpointRouteBuilder MapOpenSessionEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId, OpenRequest request, ClaimsPrincipal principal,
        IClubRepository clubs, IClubAdminRepository admins, ISessionHostRepository hosts,
        IPegboardRepository pegboard, CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(clubId, ct) is null) return Results.NotFound();
        var authz = await SessionAuthorizer.RequireSessionRunnerAsync(principal, clubId, admins, hosts, ct);
        if (authz is not null) return authz;

        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0 || name.Length > MaxNameLength)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid name");

        try
        {
            var id = await pegboard.OpenAsync(clubId, name, principal.UserId()!.Value, ct);
            return Results.Created($"/api/clubs/{clubId}/pegboard/sessions/{id}",
                new SessionDto(id, clubId, name, "Open"));
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "This club already has an open session");
        }
    }
}
