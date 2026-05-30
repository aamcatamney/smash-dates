using System.Security.Claims;
using smash_dates.Models;
using smash_dates.Repositories;

namespace smash_dates.Endpoints.Matches;

public static class RecordResultEndpoint
{
    public sealed record RecordResultRequest(int HomeScore, int AwayScore, DateOnly PlayedOn);

    public static IEndpointRouteBuilder MapRecordResultEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{id:guid}/result", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        RecordResultRequest request,
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
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Only a Confirmed match can be marked Played");

        if (request.HomeScore < 0 || request.AwayScore < 0)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Scores must be non-negative");

        var division = await divisions.GetByIdAsync(match.DivisionId, ct);
        if (division is null) return Results.NotFound();

        if (request.HomeScore + request.AwayScore != division.RubbersPerMatch)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: $"Scores must sum to {division.RubbersPerMatch} (the division's rubbers per match)");

        if (request.PlayedOn < match.MatchDate)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Played date is before the scheduled match date");

        if (!await matches.RecordResultAsync(id, request.HomeScore, request.AwayScore, request.PlayedOn, isWalkover: false, ct))
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Only a Confirmed match can be marked Played");

        return Results.Ok(new { status = MatchStatus.Played.ToString() });
    }
}
