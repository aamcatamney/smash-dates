using smash_dates.Models;

namespace smash_dates.Repositories;

public interface ILeagueAdminRepository
{
    Task<bool> IsAdminAsync(Guid leagueId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<LeagueAdminGrant>> ListByLeagueAsync(Guid leagueId, CancellationToken ct = default);
    Task<int> CountByLeagueAsync(Guid leagueId, CancellationToken ct = default);
    Task GrantAsync(Guid leagueId, Guid userId, Guid? grantedBy, CancellationToken ct = default);
    Task<bool> RevokeAsync(Guid leagueId, Guid userId, CancellationToken ct = default);
}
