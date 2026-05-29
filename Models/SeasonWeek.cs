namespace smash_dates.Models;

// A calendar range within a Season during which Matches of the matching WeekType may land.
// Order is derived from StartDate (Weeks never overlap) — see docs/adr/0002.
public sealed class SeasonWeek
{
    public Guid Id { get; init; }
    public Guid SeasonId { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public WeekType WeekType { get; init; }
}
