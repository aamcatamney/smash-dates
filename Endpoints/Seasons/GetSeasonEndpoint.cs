using smash_dates.Repositories;

namespace smash_dates.Endpoints.Seasons;

public static class GetSeasonEndpoint
{
    public sealed record WeekDetail(DateOnly StartDate, DateOnly EndDate, string WeekType);

    public sealed record SeasonDetail(
        Guid Id,
        Guid LeagueId,
        string Name,
        DateOnly StartDate,
        DateOnly EndDate,
        string Status,
        WeekDetail[] Weeks);

    public static IEndpointRouteBuilder MapGetSeasonEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{id:guid}", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid leagueId,
        Guid id,
        ISeasonRepository seasons,
        CancellationToken ct)
    {
        var season = await seasons.GetByIdAsync(id, ct);
        if (season is null || season.LeagueId != leagueId) return Results.NotFound();

        var weeks = await seasons.ListWeeksAsync(id, ct);
        return Results.Ok(new SeasonDetail(
            season.Id,
            season.LeagueId,
            season.Name,
            season.StartDate,
            season.EndDate,
            season.Status.ToString(),
            weeks.Select(w => new WeekDetail(w.StartDate, w.EndDate, w.WeekType.ToString())).ToArray()));
    }
}
