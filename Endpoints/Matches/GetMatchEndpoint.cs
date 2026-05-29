using smash_dates.Repositories;

namespace smash_dates.Endpoints.Matches;

public static class GetMatchEndpoint
{
    public sealed record MatchDetail(
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
        bool AwayAccepted);

    public static IEndpointRouteBuilder MapGetMatchEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{id:guid}", Handle);
        return app;
    }

    private static async Task<IResult> Handle(Guid id, IMatchRepository matches, CancellationToken ct)
    {
        var m = await matches.GetViewByIdAsync(id, ct);
        return m is null
            ? Results.NotFound()
            : Results.Ok(new MatchDetail(
                m.Id, m.SeasonId, m.DivisionId, m.DivisionName, m.HomeTeamId, m.HomeTeamName,
                m.AwayTeamId, m.AwayTeamName, m.VenueId, m.VenueName, m.MatchDate, m.Status.ToString(),
                m.HomeAccepted, m.AwayAccepted));
    }
}
