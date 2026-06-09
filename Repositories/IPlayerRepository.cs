using smash_dates.Models;

namespace smash_dates.Repositories;

// A player as affiliated with a club, for the club's roster view.
public sealed record PlayerClubView(Guid PlayerId, string FullName, Gender Gender, PlayerClubType Type, int? Grade);

public interface IPlayerRepository
{
    Task<Guid> CreateAsync(string fullName, Gender gender, CancellationToken ct = default);
    Task<Player?> GetByIdAsync(Guid id, CancellationToken ct = default);

    // Upserts the affiliation: links the player to the club, or changes its type.
    Task LinkAsync(Guid playerId, Guid clubId, PlayerClubType type, CancellationToken ct = default);
    Task<PlayerClub?> GetLinkAsync(Guid playerId, Guid clubId, CancellationToken ct = default);
    Task<IReadOnlyList<PlayerClubView>> ListByClubAsync(Guid clubId, CancellationToken ct = default);
    Task<bool> UnlinkAsync(Guid playerId, Guid clubId, CancellationToken ct = default);
    Task<bool> SetGradeAsync(Guid playerId, int? grade, CancellationToken ct = default);
    // Rename the global player record (gender is immutable; grade has its own setter).
    Task<bool> UpdateNameAsync(Guid playerId, string fullName, CancellationToken ct = default);
}
