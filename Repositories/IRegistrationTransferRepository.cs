using smash_dates.Models;

namespace smash_dates.Repositories;

public sealed record TransferView(
    Guid Id,
    Guid PlayerId,
    string PlayerName,
    Discipline Discipline,
    Guid FromClubId,
    string FromShortCode,
    Guid ToClubId,
    string ToShortCode,
    Guid LeagueId,
    string LeagueName,
    TransferStatus Status,
    bool ReleasingApproved,
    bool LeagueApproved);

public interface IRegistrationTransferRepository
{
    Task<Guid> CreateAsync(Guid playerId, Guid leagueId, Discipline discipline, Guid fromClubId, Guid toClubId, Guid initiatedBy, CancellationToken ct = default);
    Task<RegistrationTransfer?> GetByIdAsync(Guid id, CancellationToken ct = default);

    // Set an approval flag on a Pending transfer; returns true if BOTH approvals are now in
    // (so the caller should complete it), false if not yet, null if the transfer was not Pending.
    Task<bool?> SetReleasingApprovedAsync(Guid id, CancellationToken ct = default);
    Task<bool?> SetLeagueApprovedAsync(Guid id, CancellationToken ct = default);

    Task<bool> RejectAsync(Guid id, CancellationToken ct = default);

    // Transactionally completes the transfer: marks it Completed, moves the Confirmed
    // registration to the receiving club, and ensures a Member affiliation there.
    Task CompleteAsync(RegistrationTransfer transfer, CancellationToken ct = default);

    Task<IReadOnlyList<TransferView>> ListByClubAsync(Guid clubId, CancellationToken ct = default);
    Task<IReadOnlyList<TransferView>> ListByLeagueAsync(Guid leagueId, CancellationToken ct = default);
}
