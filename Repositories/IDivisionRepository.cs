using smash_dates.Models;

namespace smash_dates.Repositories;

public interface IDivisionRepository
{
    Task<Division?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Division>> ListByLeagueAsync(Guid leagueId, CancellationToken ct = default);
    Task<Guid> CreateAsync(
        Guid leagueId,
        string name,
        DivisionGender gender,
        int rank,
        int rubbersPerMatch,
        int winPoints,
        int drawPoints,
        int lossPoints,
        CancellationToken ct = default);
}
