using System.Security.Claims;
using smash_dates.Models;
using smash_dates.Repositories;

namespace smash_dates.Endpoints.Matches;

public static class RejectMatchEndpoint
{
    public sealed record RejectResult(string Status);

    public static IEndpointRouteBuilder MapRejectMatchEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{id:guid}/reject", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        ClaimsPrincipal principal,
        IMatchRepository matches,
        ITeamRepository teams,
        IClubAdminRepository clubAdmins,
        CancellationToken ct)
    {
        var match = await matches.GetByIdAsync(id, ct);
        if (match is null) return Results.NotFound();

        var (canHome, canAway) = await MatchActionEndpoints.ResolveSidesAsync(principal, match, teams, clubAdmins, ct);
        if (!canHome && !canAway) return Results.Forbid();

        if (match.Status != MatchStatus.Proposed)
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Only a Proposed match can be rejected");

        if (!await matches.RejectAsync(id, ct))
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Only a Proposed match can be rejected");

        return Results.Ok(new RejectResult(MatchStatus.Rejected.ToString()));
    }
}
