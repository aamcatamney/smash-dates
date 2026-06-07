namespace smash_dates.Services.Scheduling;

// A read-only "explain" of a season's scheduling, produced by a dry-run (no persistence).
// Surfaces, per division, how many matches a double round-robin needs vs how many the scheduler
// could actually place, plus the eligible-week count (a common cause of shortfall) — and the
// specific pairings left unplaced. See issue #68.
public sealed record DivisionDiagnostic(
    Guid DivisionId,
    string DivisionName,
    int Teams,
    int MatchesRequired,
    int MatchesPlaced,
    int EligibleWeeks);

public sealed record UnplacedPairingInfo(
    Guid DivisionId, string DivisionName, string HomeTeamName, string AwayTeamName);

public sealed record SchedulingDiagnostics(
    bool FullyPlaced,
    int TotalRequired,
    int TotalPlaced,
    IReadOnlyList<DivisionDiagnostic> Divisions,
    IReadOnlyList<UnplacedPairingInfo> Unplaced);
