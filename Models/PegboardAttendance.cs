namespace smash_dates.Models;

public sealed class PegboardAttendance
{
    public Guid Id { get; init; }
    public Guid SessionId { get; init; }
    public Guid? PlayerId { get; init; }
    public string? GuestName { get; init; }
    public Gender Gender { get; init; }
    public int? Grade { get; init; }
    public AttendanceStatus Status { get; init; }
    public DateTime WaitingSince { get; init; }
    public DateTime CreatedAt { get; init; }
}
