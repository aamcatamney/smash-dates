using smash_dates.Models;

namespace smash_dates.Repositories;

public interface ISessionHostRepository
{
    Task<bool> IsHostAsync(Guid clubId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<SessionHostGrant>> ListByClubAsync(Guid clubId, CancellationToken ct = default);

    // The clubs a user is a session host of, each with its name — for the user's own grants view.
    Task<IReadOnlyList<SessionHostGrantView>> ListByUserAsync(Guid userId, CancellationToken ct = default);
    Task GrantAsync(Guid clubId, Guid userId, Guid? grantedBy, CancellationToken ct = default);
    Task<bool> RevokeAsync(Guid clubId, Guid userId, CancellationToken ct = default);
}
