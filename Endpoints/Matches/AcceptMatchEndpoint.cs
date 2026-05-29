using System.Security.Claims;
using smash_dates.Models;
using smash_dates.Repositories;

namespace smash_dates.Endpoints.Matches;

public static class AcceptMatchEndpoint
{
    public sealed record AcceptResult(string Status, bool HomeAccepted, bool AwayAccepted);

    public static IEndpointRouteBuilder MapAcceptMatchEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{id:guid}/accept", Handle);
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
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Only a Proposed match can be accepted");

        if (!await matches.ApplyAcceptAsync(id, canHome, canAway, ct))
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Only a Proposed match can be accepted");

        var updated = await matches.GetByIdAsync(id, ct);
        return Results.Ok(new AcceptResult(updated!.Status.ToString(), updated.HomeAccepted, updated.AwayAccepted));
    }
}
