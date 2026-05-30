using smash_dates.Models;

namespace smash_dates.Services.Scheduling;

public sealed record SchedulerTeam(Guid Id, Guid ClubId);

public sealed record SchedulerDivision(Guid Id, DivisionGender Gender, IReadOnlyList<SchedulerTeam> Teams);

public sealed record SchedulerWeek(DateOnly Start, DateOnly End, WeekType Type);

public sealed record SchedulerVenue(Guid Id, Guid ClubId, int Capacity);

public sealed record SchedulerBlock(
    BlockedDateScope Scope, Guid ClubId, Guid? VenueId, Guid? TeamId, DateOnly Start, DateOnly End);

// A fixture already Confirmed and held fixed during an incremental re-run.
public sealed record LockedMatch(Guid DivisionId, Guid HomeTeamId, Guid AwayTeamId, Guid VenueId, DateOnly Date);

// Per-League soft-penalty configuration. TargetGapDays null = derive ~half-season.
public sealed record SchedulerWeights(int SpreadWeight, int LegWeight, int MinGapDays, int? TargetGapDays)
{
    public static readonly SchedulerWeights Default = new(2, 1, 7, null);
}

public sealed record SchedulerInput(
    IReadOnlyList<SchedulerDivision> Divisions,
    IReadOnlyList<SchedulerWeek> Weeks,
    IReadOnlyList<SchedulerVenue> Venues,
    IReadOnlyList<SchedulerBlock> Blocks)
{
    // Pre-placed Confirmed fixtures: their slots are occupied and their pairings are not
    // re-emitted. Empty for a from-scratch generate.
    public IReadOnlyList<LockedMatch> Locked { get; init; } = [];

    // Soft-penalty weights for the optimiser; defaults match the out-of-the-box values.
    public SchedulerWeights Weights { get; init; } = SchedulerWeights.Default;
}

public sealed record ScheduledMatch(Guid DivisionId, Guid HomeTeamId, Guid AwayTeamId, Guid VenueId, DateOnly Date);

public sealed record UnplacedPairing(Guid DivisionId, Guid HomeTeamId, Guid AwayTeamId);

public sealed record ScheduleResult(
    bool Success,
    IReadOnlyList<ScheduledMatch> Matches,
    IReadOnlyList<UnplacedPairing> Unplaced);

public interface IScheduler
{
    ScheduleResult Build(SchedulerInput input);
}
