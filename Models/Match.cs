namespace smash_dates.Models;

// A tie between two Teams on a single night, placed by the scheduler on a (Venue, Date)
// slot. The Match is atomic; rubber-level scoring is out of scope.
public sealed class Match
{
    public Guid Id { get; init; }
    public Guid SeasonId { get; init; }
    public Guid DivisionId { get; init; }
    public Guid HomeTeamId { get; init; }
    public Guid AwayTeamId { get; init; }
    public Guid VenueId { get; init; }
    public DateOnly MatchDate { get; init; }
    public MatchStatus Status { get; init; }
    public bool HomeAccepted { get; init; }
    public bool AwayAccepted { get; init; }
    public int? HomeScore { get; init; }
    public int? AwayScore { get; init; }
    public DateOnly? PlayedOn { get; init; }
    public bool IsWalkover { get; init; }
    public DateTime CreatedAt { get; init; }
}
