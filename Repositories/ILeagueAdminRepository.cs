using smash_dates.Models;

namespace smash_dates.Repositories;

public interface ILeagueAdminRepository
{
    Task<bool> IsAdminAsync(Guid leagueId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<LeagueAdminGrant>> ListByLeagueAsync(Guid leagueId, CancellationToken ct = default);

    // The leagues a user is an admin of, each with its name — for the user's own grants view.
    Task<IReadOnlyList<LeagueAdminGrantView>> ListByUserAsync(Guid userId, CancellationToken ct = default);
    Task<int> CountByLeagueAsync(Guid leagueId, CancellationToken ct = default);
    Task GrantAsync(Guid leagueId, Guid userId, Guid? grantedBy, CancellationToken ct = default);
    Task<bool> RevokeAsync(Guid leagueId, Guid userId, CancellationToken ct = default);

    /// Atomically revokes a grant unless it would leave the league with zero admins.
    /// Returns Revoked, NotAdmin, or WouldBeLastAdmin. Race-safe.
    Task<RevokeResult> RevokeUnlessLastAsync(Guid leagueId, Guid userId, CancellationToken ct = default);
}

public enum RevokeResult
{
    Revoked,
    NotAdmin,
    WouldBeLastAdmin,
}
