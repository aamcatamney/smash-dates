using smash_dates.Models;

namespace smash_dates.Repositories;

// A league plus at-a-glance stats for the leagues list: number of divisions, number of
// distinct players with a Confirmed registration, and the current Active season's name (if any).
public sealed record LeagueListItem(
    Guid Id, string Name, string? Description, int DivisionCount, int PlayerCount, string? ActiveSeasonName);

public interface ILeagueRepository
{
    Task<League?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<League>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyList<LeagueListItem>> ListSummariesAsync(CancellationToken ct = default);
    Task<Guid> CreateAsync(string name, string? description, Guid createdBy, CancellationToken ct = default);

    // Creates the league row and the initial LeagueAdmin grant in a single transaction.
    Task<Guid> CreateWithFirstAdminAsync(
        string name,
        string? description,
        Guid createdBy,
        Guid firstAdminUserId,
        CancellationToken ct = default);

    Task<bool> UpdateSchedulingConfigAsync(
        Guid id, int spreadWeight, int legWeight, int minGapDays, int? targetGapDays, int courtsPerMatch, CancellationToken ct = default);
}
