using smash_dates.Models;

namespace smash_dates.Repositories;

// Flat projection for building scheduler input: each entered team with its division,
// the division's gender, and the team's owning club.
public sealed record SchedulingEntry(Guid DivisionId, DivisionGender Gender, Guid TeamId, Guid ClubId);

public interface ISeasonEntryRepository
{
    Task<SeasonEntry?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SeasonEntryView>> ListBySeasonAsync(Guid seasonId, CancellationToken ct = default);
    Task<IReadOnlyList<SchedulingEntry>> ListForSchedulingAsync(Guid seasonId, CancellationToken ct = default);
    Task<Guid> CreateAsync(Guid seasonId, Guid divisionId, Guid teamId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsForTeamAsync(Guid teamId, CancellationToken ct = default);
}
