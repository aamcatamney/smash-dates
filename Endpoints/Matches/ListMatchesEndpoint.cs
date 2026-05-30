using smash_dates.Repositories;

namespace smash_dates.Endpoints.Matches;

public static class ListMatchesEndpoint
{
    public sealed record MatchSummary(
        Guid Id,
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

    public static IEndpointRouteBuilder MapListMatchesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/matches", Handle);
        return app;
    }

    private static async Task<IResult> Handle(Guid seasonId, IMatchRepository matches, CancellationToken ct)
    {
        var rows = await matches.ListBySeasonAsync(seasonId, ct);
        return Results.Ok(rows
            .Select(m => new MatchSummary(
                m.Id, m.DivisionId, m.DivisionName, m.HomeTeamId, m.HomeTeamName,
                m.AwayTeamId, m.AwayTeamName, m.VenueId, m.VenueName, m.MatchDate, m.Status.ToString(),
                m.HomeAccepted, m.AwayAccepted, m.HomeScore, m.AwayScore, m.IsWalkover))
            .ToArray());
    }
}
