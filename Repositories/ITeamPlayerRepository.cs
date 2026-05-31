using smash_dates.Models;

namespace smash_dates.Repositories;

public sealed record SquadMember(Guid PlayerId, string FullName, Gender Gender);

public interface ITeamPlayerRepository
{
    Task<IReadOnlyList<SquadMember>> ListByTeamAsync(Guid teamId, CancellationToken ct = default);
    Task AddAsync(Guid teamId, Guid playerId, CancellationToken ct = default);
    Task<bool> RemoveAsync(Guid teamId, Guid playerId, CancellationToken ct = default);

    // True if the player holds a Confirmed registration for `discipline` at `clubId` in a
    // league the team is currently (non-Closed season) entered in. Gender is checked separately.
    Task<bool> IsEligibleAsync(Guid playerId, Guid clubId, Guid teamId, Discipline discipline, CancellationToken ct = default);
}
