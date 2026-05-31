namespace smash_dates.Models;

public sealed class PegboardSession
{
    public Guid Id { get; init; }
    public Guid ClubId { get; init; }
    public string Name { get; init; } = string.Empty;
    public PegboardSessionStatus Status { get; init; }
    public Guid? OpenedBy { get; init; }
    public DateTime OpenedAt { get; init; }
    public DateTime? ClosedAt { get; init; }
}
