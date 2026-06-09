using smash_dates.Models;

namespace smash_dates.Repositories;

public interface IVenueRepository
{
    Task<Venue?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Venue>> ListByClubAsync(Guid clubId, CancellationToken ct = default);
    Task<Guid> CreateAsync(Guid clubId, string name, int courts, int maxConcurrentMatches, string? address, CancellationToken ct = default);
    Task<bool> UpdateAsync(Guid id, string name, int courts, int maxConcurrentMatches, string? address, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
