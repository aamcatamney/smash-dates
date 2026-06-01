namespace smash_dates.Models;

// A global, admin-managed person (no login). See docs/adr/0003.
public sealed class Player
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public Gender Gender { get; init; }
    public int? Grade { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
