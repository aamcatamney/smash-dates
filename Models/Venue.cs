namespace smash_dates.Models;

// A physical hall belonging to a Club. Court capacity is 1 or 2 simultaneous Matches
// per slot. Unavailable dates (the VenueBlocked scope) are a separate later slice.
public sealed class Venue
{
    public Guid Id { get; init; }
    public Guid ClubId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Capacity { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
