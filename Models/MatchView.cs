namespace smash_dates.Models;

// Read model for listing a season's matches with related names for display.
public sealed class MatchView
{
    public Guid Id { get; init; }
    public Guid SeasonId { get; init; }
    public Guid DivisionId { get; init; }
    public string DivisionName { get; init; } = string.Empty;
    public Guid HomeTeamId { get; init; }
    public string HomeTeamName { get; init; } = string.Empty;
    public Guid AwayTeamId { get; init; }
    public string AwayTeamName { get; init; } = string.Empty;
    public Guid VenueId { get; init; }
    public string VenueName { get; init; } = string.Empty;
    public DateOnly MatchDate { get; init; }
    public MatchStatus Status { get; init; }
    public bool HomeAccepted { get; init; }
    public bool AwayAccepted { get; init; }
    public int? HomeScore { get; init; }
    public int? AwayScore { get; init; }
    public DateOnly? PlayedOn { get; init; }
    public bool IsWalkover { get; init; }
}
