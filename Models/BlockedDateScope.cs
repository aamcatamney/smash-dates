namespace smash_dates.Models;

public enum BlockedDateScope
{
    // No Team of the Club plays (club AGM, social night).
    Club,

    // A specific Venue is unavailable (other booking, maintenance).
    Venue,

    // A specific Team cannot play (player exams, holiday).
    Team,
}
