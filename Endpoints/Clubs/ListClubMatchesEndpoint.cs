using smash_dates.Repositories;

namespace smash_dates.Endpoints.Clubs;

public static class ListClubMatchesEndpoint
{
    public sealed record ClubMatchSummary(
        Guid Id,
        Guid SeasonId,
        Guid DivisionId,
        string DivisionName,
        Guid HomeTeamId,
        string HomeTeamName,
        Guid AwayTeamId,
        string AwayTeamName,
        Guid VenueId,
        string VenueName,
        DateOnly MatchDate,
        string Status,
        bool HomeAccepted,
        bool AwayAccepted,
        int? HomeScore,
        int? AwayScore,
        bool IsWalkover);

    public static IEndpointRouteBuilder MapListClubMatchesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{clubId:guid}/matches", Handle);
        return app;
    }

    private static async Task<IResult> Handle(Guid clubId, IMatchRepository matches, CancellationToken ct)
    {
        var rows = await matches.ListByClubAsync(clubId, ct);
        return Results.Ok(rows
            .Select(m => new ClubMatchSummary(
                m.Id, m.SeasonId, m.DivisionId, m.DivisionName, m.HomeTeamId, m.HomeTeamName,
                m.AwayTeamId, m.AwayTeamName, m.VenueId, m.VenueName, m.MatchDate, m.Status.ToString(),
                m.HomeAccepted, m.AwayAccepted, m.HomeScore, m.AwayScore, m.IsWalkover))
            .ToArray());
    }
}
