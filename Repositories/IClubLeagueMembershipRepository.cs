using smash_dates.Models;

namespace smash_dates.Repositories;

public interface IClubLeagueMembershipRepository
{
    Task<ClubLeagueMembership?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ClubLeagueMembership>> ListByLeagueAsync(Guid leagueId, CancellationToken ct = default);
    Task<IReadOnlyList<ClubLeagueMembership>> ListByClubAsync(Guid clubId, CancellationToken ct = default);

    /// Creates a Pending row. Throws PostgresException SQLSTATE 23505 if a non-terminal
    /// (Pending or Accepted) membership already exists for (club, league).
    Task<Guid> InviteAsync(Guid clubId, Guid leagueId, Guid invitedBy, CancellationToken ct = default);

    /// Transitions Pending → newStatus (Accepted | Declined). Returns false if the row
    /// wasn't Pending. Used for accept/decline.
    Task<bool> TransitionFromPendingAsync(
        Guid membershipId,
        MembershipStatus newStatus,
        Guid respondedBy,
        CancellationToken ct = default);

    /// Transitions Accepted → newStatus (Withdrawn | Expelled). Returns false if the
    /// row wasn't Accepted. Used for withdraw/expel.
    Task<bool> TransitionFromAcceptedAsync(
        Guid membershipId,
        MembershipStatus newStatus,
        Guid respondedBy,
        CancellationToken ct = default);
}
