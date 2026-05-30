using smash_dates.Models;

namespace smash_dates.Repositories;

public interface ITeamRepository
{
    Task<Team?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Team>> ListByClubAsync(Guid clubId, CancellationToken ct = default);

    // Teams of every club with an Accepted membership in the league — used to resolve
    // team names when bulk-importing season entries.
    Task<IReadOnlyList<Team>> ListByLeagueAcceptedMembersAsync(Guid leagueId, CancellationToken ct = default);
    Task<Guid> CreateAsync(Guid clubId, string name, DivisionGender gender, CancellationToken ct = default);

    // Name only — gender is immutable after creation (see CONTEXT.md).
    Task<bool> UpdateNameAsync(Guid id, string name, CancellationToken ct = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
