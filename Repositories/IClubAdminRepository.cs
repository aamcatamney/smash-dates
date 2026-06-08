using smash_dates.Models;

namespace smash_dates.Repositories;

public interface IClubAdminRepository
{
    Task<bool> IsAdminAsync(Guid clubId, Guid userId, CancellationToken ct = default);

    // True if the user is a ClubAdmin of at least one club — gates cross-club player search.
    Task<bool> IsAdminOfAnyClubAsync(Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<ClubAdminGrant>> ListByClubAsync(Guid clubId, CancellationToken ct = default);

    // The clubs a user is an admin of, each with its name — for the user's own grants view.
    Task<IReadOnlyList<ClubAdminGrantView>> ListByUserAsync(Guid userId, CancellationToken ct = default);
    Task GrantAsync(Guid clubId, Guid userId, Guid? grantedBy, CancellationToken ct = default);
    Task<bool> RevokeAsync(Guid clubId, Guid userId, CancellationToken ct = default);
    Task<RevokeResult> RevokeUnlessLastAsync(Guid clubId, Guid userId, CancellationToken ct = default);
}
