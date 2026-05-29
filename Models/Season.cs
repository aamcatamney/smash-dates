namespace smash_dates.Models;

public sealed class Season
{
    public Guid Id { get; init; }
    public Guid LeagueId { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public SeasonStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
