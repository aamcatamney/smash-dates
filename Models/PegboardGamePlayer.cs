namespace smash_dates.Models;

public sealed class PegboardGamePlayer
{
    public Guid GameId { get; init; }
    public Guid AttendanceId { get; init; }
    public GameSide Side { get; init; }
}
