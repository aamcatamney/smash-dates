using smash_dates.Models;

namespace smash_dates.Repositories;

public sealed record SquadMember(Guid PlayerId, string FullName, Gender Gender);

public interface ITeamPlayerRepository
{
    Task<IReadOnlyList<SquadMember>> ListByTeamAsync(Guid teamId, CancellationToken ct = default);
    Task AddAsync(Guid teamId, Guid playerId, CancellationToken ct = default);
    Task<bool> RemoveAsync(Guid teamId, Guid playerId, CancellationToken ct = default);

    // True if the player holds a Confirmed registration for `discipline` at `clubId` in any
    // league. Gender is checked separately. (A squad can be built before the team is entered
    // in a season, so eligibility is not tied to the team's current league entries.)
    Task<bool> IsEligibleAsync(Guid playerId, Guid clubId, Discipline discipline, CancellationToken ct = default);
}
