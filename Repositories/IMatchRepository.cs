using smash_dates.Models;
using smash_dates.Services.Scheduling;

namespace smash_dates.Repositories;

public interface IMatchRepository
{
    Task<IReadOnlyList<MatchView>> ListBySeasonAsync(Guid seasonId, CancellationToken ct = default);
    Task<Match?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<MatchView?> GetViewByIdAsync(Guid id, CancellationToken ct = default);

    // Inserts the generated Proposed matches and transitions the season Draft → Proposed
    // in a single transaction.
    Task InsertScheduleAsync(Guid seasonId, IReadOnlyList<ScheduledMatch> matches, CancellationToken ct = default);

    // Incremental re-run: deletes the season's Proposed + Rejected matches and inserts the
    // re-placed ones as Proposed, in one transaction. Confirmed matches are left untouched.
    Task ReplaceProposedAndRejectedAsync(Guid seasonId, IReadOnlyList<ScheduledMatch> matches, CancellationToken ct = default);

    Task<bool> ExistsForVenueAsync(Guid venueId, CancellationToken ct = default);

    // Records acceptance for the given side(s); confirms the match when both sides accept.
    // Only acts on a Proposed match; returns false if the match was not Proposed.
    Task<bool> ApplyAcceptAsync(Guid id, bool acceptHome, bool acceptAway, CancellationToken ct = default);

    // Proposed → Rejected. Returns false if the match was not Proposed.
    Task<bool> RejectAsync(Guid id, CancellationToken ct = default);

    // Proposed → Confirmed (LeagueAdmin override). Returns false if the match was not Proposed.
    Task<bool> ForceConfirmAsync(Guid id, CancellationToken ct = default);
}
