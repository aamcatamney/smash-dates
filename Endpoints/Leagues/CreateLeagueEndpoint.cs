using System.Security.Claims;
using Npgsql;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Leagues;

public static class CreateLeagueEndpoint
{
    private const int MaxNameLength = 200;
    private const int MaxDescriptionLength = 2000;
    private const string DuplicateNameSqlState = "23505";

    public sealed record CreateLeagueRequest(string Name, string? Description);
    public sealed record LeagueResponse(Guid Id, string Name, string? Description);

    public static IEndpointRouteBuilder MapCreateLeagueEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("", Handle)
            .RequireAuthorization(AuthorizationPolicies.SystemAdmin);
        return app;
    }

    private static async Task<IResult> Handle(
        CreateLeagueRequest request,
        ClaimsPrincipal principal,
        ILeagueRepository leagues,
        CancellationToken ct)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0 || name.Length > MaxNameLength)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid name");
        }

        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description!.Trim();
        if (description is { Length: > MaxDescriptionLength })
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Description too long");
        }

        var createdBy = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

        try
        {
            var id = await leagues.CreateAsync(name, description, createdBy, ct);
            return Results.Created($"/api/leagues/{id}", new LeagueResponse(id, name, description));
        }
        catch (PostgresException ex) when (ex.SqlState == DuplicateNameSqlState)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "League name already in use");
        }
    }
}
