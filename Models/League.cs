namespace smash_dates.Models;

public sealed class League
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid CreatedBy { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }

    // Scheduler soft-penalty configuration (see SchedulerWeights). TargetGapDays null =
    // derive ~half-season.
    public int SpreadWeight { get; init; }
    public int LegWeight { get; init; }
    public int MinGapDays { get; init; }
    public int? TargetGapDays { get; init; }

    // How many courts one Match occupies (rubbers run in parallel); a Venue's slot capacity
    // is floor(courts / CourtsPerMatch), capped by its MaxConcurrentMatches. Default 2.
    public int CourtsPerMatch { get; init; }
}
