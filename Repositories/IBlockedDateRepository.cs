using smash_dates.Models;

namespace smash_dates.Repositories;

public interface IBlockedDateRepository
{
    Task<BlockedDate?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<BlockedDate>> ListByClubAsync(Guid clubId, CancellationToken ct = default);
    Task<Guid> CreateAsync(
        Guid clubId,
        BlockedDateScope scope,
        Guid? venueId,
        Guid? teamId,
        DateOnly startDate,
        DateOnly endDate,
        string reason,
        CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
