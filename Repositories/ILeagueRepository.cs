using smash_dates.Models;

namespace smash_dates.Repositories;

public interface ILeagueRepository
{
    Task<League?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<League>> ListAsync(CancellationToken ct = default);
    Task<Guid> CreateAsync(string name, string? description, Guid createdBy, CancellationToken ct = default);
}
