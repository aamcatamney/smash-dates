using smash_dates.Models;

namespace smash_dates.Repositories;

public interface IClubRepository
{
    Task<Club?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Club?> GetByShortCodeAsync(string shortCode, CancellationToken ct = default);
    Task<IReadOnlyList<Club>> ListAsync(CancellationToken ct = default);

    // Creates the club row and the initial ClubAdmin grant in a single transaction.
    Task<Guid> CreateWithFirstAdminAsync(
        string name,
        string shortCode,
        string contactEmail,
        string? notes,
        Guid firstAdminUserId,
        Guid grantedBy,
        CancellationToken ct = default);

    Task<bool> UpdateAsync(
        Guid id,
        string name,
        string shortCode,
        string contactEmail,
        string? notes,
        CancellationToken ct = default);
}
