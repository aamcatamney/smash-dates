namespace smash_dates.Models;

public sealed class Club
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ShortCode { get; init; } = string.Empty;
    public string ContactEmail { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
