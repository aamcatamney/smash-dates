namespace smash_dates.Models;

public sealed class PegboardGame
{
    public Guid Id { get; init; }
    public Guid SessionId { get; init; }
    public Guid CourtId { get; init; }
    public GameType Type { get; init; }
    public GameStatus Status { get; init; }
    public GameSide? WinnerSide { get; init; }
    public string? Score { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? EndedAt { get; init; }
}
