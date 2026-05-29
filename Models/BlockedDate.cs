namespace smash_dates.Models;

// A (StartDate, EndDate, Reason) range during which one scope cannot host or play Matches.
// Single-day blocks have StartDate == EndDate. Owned by the Club admin in all three scopes.
public sealed class BlockedDate
{
    public Guid Id { get; init; }
    public Guid ClubId { get; init; }
    public BlockedDateScope Scope { get; init; }
    public Guid? VenueId { get; init; }
    public Guid? TeamId { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
