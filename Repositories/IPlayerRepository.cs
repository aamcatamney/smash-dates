using smash_dates.Models;

namespace smash_dates.Repositories;

// A player as affiliated with a club, for the club's roster view.
public sealed record PlayerClubView(Guid PlayerId, string FullName, Gender Gender, PlayerClubType Type, int? Grade);

public interface IPlayerRepository
{
    Task<Guid> CreateAsync(string fullName, Gender gender, CancellationToken ct = default);
    Task<Player?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Player>> SearchAsync(string query, int limit, CancellationToken ct = default);
    // Exact (case-insensitive name + gender) matches, used by the bulk import to reuse an
    // existing global player. More than one match means the name+gender is ambiguous.
    Task<IReadOnlyList<Player>> FindByNameAndGenderAsync(string fullName, Gender gender, CancellationToken ct = default);

    // Upserts the affiliation: links the player to the club, or changes its type.
    Task LinkAsync(Guid playerId, Guid clubId, PlayerClubType type, CancellationToken ct = default);
    Task<PlayerClub?> GetLinkAsync(Guid playerId, Guid clubId, CancellationToken ct = default);
    Task<IReadOnlyList<PlayerClubView>> ListByClubAsync(Guid clubId, CancellationToken ct = default);
    Task<bool> UnlinkAsync(Guid playerId, Guid clubId, CancellationToken ct = default);
    Task<bool> SetGradeAsync(Guid playerId, int? grade, CancellationToken ct = default);
}
