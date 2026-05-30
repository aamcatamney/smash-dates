using System.Security.Claims;
using smash_dates.Models;
using smash_dates.Repositories;

namespace smash_dates.Endpoints.Matches;

public static class RecordWalkoverEndpoint
{
    public sealed record WalkoverRequest(string Winner);

    public static IEndpointRouteBuilder MapRecordWalkoverEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{id:guid}/walkover", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        WalkoverRequest request,
        ClaimsPrincipal principal,
        IMatchRepository matches,
        ITeamRepository teams,
        IClubAdminRepository clubAdmins,
        IDivisionRepository divisions,
        CancellationToken ct)
    {
        var match = await matches.GetByIdAsync(id, ct);
        if (match is null) return Results.NotFound();

        var (canHome, canAway) = await MatchActionEndpoints.ResolveSidesAsync(principal, match, teams, clubAdmins, ct);
        if (!canHome && !canAway) return Results.Forbid();

        if (match.Status != MatchStatus.Confirmed)
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Only a Confirmed match can be recorded as a walkover");

        var homeWins = string.Equals(request.Winner, "Home", StringComparison.OrdinalIgnoreCase);
        var awayWins = string.Equals(request.Winner, "Away", StringComparison.OrdinalIgnoreCase);
        if (!homeWins && !awayWins)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Winner must be 'Home' or 'Away'");

        var division = await divisions.GetByIdAsync(match.DivisionId, ct);
        if (division is null) return Results.NotFound();

        var homeScore = homeWins ? division.RubbersPerMatch : 0;
        var awayScore = awayWins ? division.RubbersPerMatch : 0;

        // A walkover carries no agreed play date; record it on the scheduled date.
        if (!await matches.RecordResultAsync(id, homeScore, awayScore, match.MatchDate, isWalkover: true, ct))
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Only a Confirmed match can be recorded as a walkover");

        return Results.Ok(new { status = MatchStatus.Played.ToString() });
    }
}
