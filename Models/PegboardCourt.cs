namespace smash_dates.Models;

public sealed class PegboardCourt
{
    public Guid Id { get; init; }
    public Guid SessionId { get; init; }
    public string Label { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
