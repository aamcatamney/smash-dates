using smash_dates.Models;

namespace smash_dates.Repositories;

// A Confirmed registration offered as a transfer-in candidate: the player, the league the
// registration lives in, its discipline, and the club currently holding it.
public sealed record TransferCandidate(
    Guid PlayerId,
    string FullName,
    Gender Gender,
    Guid LeagueId,
    string LeagueName,
    Discipline Discipline,
    string CurrentClubShortCode);

// A registration enriched with player/club/league display fields.
public sealed record RegistrationView(
    Guid Id,
    Guid PlayerId,
    string PlayerName,
    Gender Gender,
    Guid ClubId,
    string ClubShortCode,
    Guid LeagueId,
    string LeagueName,
    Discipline Discipline,
    RegistrationStatus Status);

public interface IDisciplineRegistrationRepository
{
    Task<Guid> CreateAsync(Guid playerId, Guid clubId, Guid leagueId, Discipline discipline, Guid requestedBy, CancellationToken ct = default);
    Task<DisciplineRegistration?> GetByIdAsync(Guid id, CancellationToken ct = default);

    // The club currently holding the Confirmed registration for this triple, if any.
    Task<DisciplineRegistration?> GetConfirmedAsync(Guid playerId, Guid leagueId, Discipline discipline, CancellationToken ct = default);

    // Promote a Pending registration to Confirmed. Throws PostgresException 23505 (via the
    // partial unique index) if another club already holds it. Returns false if not Pending.
    Task<bool> ConfirmAsync(Guid id, Guid respondedBy, CancellationToken ct = default);
    Task<bool> RejectAsync(Guid id, Guid respondedBy, CancellationToken ct = default);

    Task<IReadOnlyList<RegistrationView>> ListByLeagueAsync(Guid leagueId, CancellationToken ct = default);
    Task<IReadOnlyList<RegistrationView>> ListByClubAsync(Guid clubId, CancellationToken ct = default);

    // Confirmed registrations a club may transfer in: held by another club, in a league the
    // receiving club is an Accepted member of, with the player's name matching `query`. This is
    // the only cross-club player lookup, and it is scoped to the receiving club's leagues.
    Task<IReadOnlyList<TransferCandidate>> SearchTransferCandidatesAsync(Guid receivingClubId, string query, int limit, CancellationToken ct = default);
}
