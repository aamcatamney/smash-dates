namespace smash_dates.Models;

// Gender reuses DivisionGender: a Team's gender is the same closed set as a Division's
// and must match the Divisions the Team plays in. Fixed at creation (see CONTEXT.md).
public sealed class Team
{
    public Guid Id { get; init; }
    public Guid ClubId { get; init; }
    public string Name { get; init; } = string.Empty;
    public DivisionGender Gender { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
