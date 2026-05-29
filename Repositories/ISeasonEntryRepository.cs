using smash_dates.Models;

namespace smash_dates.Repositories;

public interface ISeasonEntryRepository
{
    Task<SeasonEntry?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SeasonEntryView>> ListBySeasonAsync(Guid seasonId, CancellationToken ct = default);
    Task<Guid> CreateAsync(Guid seasonId, Guid divisionId, Guid teamId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsForTeamAsync(Guid teamId, CancellationToken ct = default);
}
