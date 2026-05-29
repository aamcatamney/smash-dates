namespace smash_dates.Models;

// A per-Season assignment placing a Team into a Division for that Season.
// Teams promote/relegate between Divisions across Seasons without losing identity.
public sealed class SeasonEntry
{
    public Guid Id { get; init; }
    public Guid SeasonId { get; init; }
    public Guid DivisionId { get; init; }
    public Guid TeamId { get; init; }
    public DateTime CreatedAt { get; init; }
}
