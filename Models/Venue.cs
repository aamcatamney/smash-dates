namespace smash_dates.Models;

// A physical hall belonging to a Club. It has a number of physical Courts and its own
// ceiling (MaxConcurrentMatches, 1 or 2) on how many Matches may run at once. The simultaneous-
// match capacity of a slot is derived from these plus the League's courts-per-match rule
// (see VenueSlotCapacity). Unavailable dates (the VenueBlocked scope) are a separate slice.
public sealed class Venue
{
    public Guid Id { get; init; }
    public Guid ClubId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Courts { get; init; }
    public int MaxConcurrentMatches { get; init; }
    // Optional free-text address, linked to a map provider in the UI. Scheduler ignores it.
    public string? Address { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
