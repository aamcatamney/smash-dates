namespace smash_dates.Models;

public sealed class Division
{
    public Guid Id { get; init; }
    public Guid LeagueId { get; init; }
    public string Name { get; init; } = string.Empty;
    public DivisionGender Gender { get; init; }
    public int Rank { get; init; }
    public int RubbersPerMatch { get; init; }
    public int WinPoints { get; init; }
    public int DrawPoints { get; init; }
    public int LossPoints { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
