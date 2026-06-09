using smash_dates.Models;

namespace smash_dates.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<Guid> CreateAsync(string email, string passwordHash, string? displayName, CancellationToken ct = default);
    Task<bool> UpdatePasswordAsync(Guid id, string passwordHash, CancellationToken ct = default);
    Task<bool> UpdateDisplayNameAsync(Guid id, string? displayName, CancellationToken ct = default);
    Task<bool> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default);
    Task<bool> SetEmailVerifiedAsync(Guid id, CancellationToken ct = default);
}
