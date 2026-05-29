namespace smash_dates.Models;

// Lifecycle: Proposed → Confirmed → Played | Postponed → Rejected.
// Only Proposed is produced by the first scheduler slice; the rest land with the
// match-confirmation lifecycle slice.
public enum MatchStatus
{
    Proposed,
    Confirmed,
    Played,
    Postponed,
    Rejected,
}
