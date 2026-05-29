using smash_dates.Models;

namespace smash_dates.Repositories;

public interface ISeasonRepository
{
    Task<Season?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Season>> ListByLeagueAsync(Guid leagueId, CancellationToken ct = default);
    Task<IReadOnlyList<SeasonWeek>> ListWeeksAsync(Guid seasonId, CancellationToken ct = default);

    // Creates the season and its weeks in a single transaction.
    Task<Guid> CreateWithWeeksAsync(
        Guid leagueId,
        string name,
        DateOnly startDate,
        DateOnly endDate,
        IReadOnlyList<(DateOnly StartDate, DateOnly EndDate, WeekType WeekType)> weeks,
        CancellationToken ct = default);

    // Replaces the season's entire week list in a single transaction.
    Task ReplaceWeeksAsync(
        Guid seasonId,
        IReadOnlyList<(DateOnly StartDate, DateOnly EndDate, WeekType WeekType)> weeks,
        CancellationToken ct = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
