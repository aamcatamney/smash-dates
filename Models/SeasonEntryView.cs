namespace smash_dates.Models;

// Read model for listing a Season's entries with the related Team/Division names.
public sealed class SeasonEntryView
{
    public Guid Id { get; init; }
    public Guid SeasonId { get; init; }
    public Guid DivisionId { get; init; }
    public string DivisionName { get; init; } = string.Empty;
    public Guid TeamId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public DivisionGender Gender { get; init; }
}
