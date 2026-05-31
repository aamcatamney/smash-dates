using smash_dates.Models;

namespace smash_dates.Repositories;

public interface ISeasonRepository
{
    Task<Season?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Season>> ListByLeagueAsync(Guid leagueId, CancellationToken ct = default);
    Task<IReadOnlyList<Season>> ListByStatusAsync(SeasonStatus status, CancellationToken ct = default);
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

    // Moves the season from one status to another only if it is currently `from`.
    // Returns false otherwise (caller maps to 409).
    Task<bool> TransitionStatusAsync(Guid id, SeasonStatus from, SeasonStatus to, CancellationToken ct = default);

    // Draft -> Scheduling, clearing any prior scheduling error. False if not Draft (race).
    Task<bool> BeginSchedulingAsync(Guid id, CancellationToken ct = default);

    // Scheduling -> Draft, recording why generation failed.
    Task FailSchedulingAsync(Guid id, string error, CancellationToken ct = default);
}
