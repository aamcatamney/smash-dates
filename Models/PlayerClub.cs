namespace smash_dates.Models;

public sealed class PlayerClub
{
    public Guid Id { get; init; }
    public Guid PlayerId { get; init; }
    public Guid ClubId { get; init; }
    public PlayerClubType Type { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
