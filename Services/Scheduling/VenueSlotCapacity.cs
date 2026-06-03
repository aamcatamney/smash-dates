namespace smash_dates.Services.Scheduling;

// How many Matches a Venue can host at once in a single (Venue, Date) slot.
// A Match needs `courtsPerMatch` courts (a per-League rule), so the courts allow
// floor(courts / courtsPerMatch) matches — then the Venue's own maxConcurrentMatches caps it.
public static class VenueSlotCapacity
{
    public static int Compute(int courts, int maxConcurrentMatches, int courtsPerMatch)
    {
        if (courtsPerMatch <= 0) return 0;
        var byCourts = courts / courtsPerMatch;
        return Math.Max(0, Math.Min(maxConcurrentMatches, byCourts));
    }
}
