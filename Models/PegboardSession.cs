namespace smash_dates.Models;

public sealed class PegboardSession
{
    public Guid Id { get; init; }
    public Guid ClubId { get; init; }
    public string Name { get; init; } = string.Empty;
    public PegboardSessionStatus Status { get; init; }
    public Guid? OpenedBy { get; init; }
    // Null while the session is still Scheduled; set when it is opened.
    public DateTime? OpenedAt { get; init; }
    public DateTime? ClosedAt { get; init; }

    // Planning fields (a Scheduled session is defined by ScheduledDate; the rest are optional).
    public DateOnly? ScheduledDate { get; init; }
    public TimeOnly? StartTime { get; init; }
    public int? DurationMinutes { get; init; }
    public Guid? VenueId { get; init; }
}
