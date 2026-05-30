using smash_dates.Models;
using smash_dates.Services.Scheduling;

namespace smash_dates.UnitTests.Services.Scheduling;

public sealed class SchedulerCostTests
{
    private static SchedulerWeek Level(string start, string end) =>
        new(DateOnly.Parse(start), DateOnly.Parse(end), WeekType.Level);

    private static SchedulerInput Input(IReadOnlyList<SchedulerWeek> weeks) =>
        new([], weeks, [], []);

    [Fact]
    public void SpreadPenalty_PenalisesCloselySpacedMatchesForATeam()
    {
        var div = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var venue = Guid.NewGuid();

        // Team A plays on Sep 1 and Sep 4 — a 3-day gap, below the ideal minimum.
        var placements = new[]
        {
            new ScheduledMatch(div, a, b, venue, new DateOnly(2025, 9, 1)),
            new ScheduledMatch(div, a, c, venue, new DateOnly(2025, 9, 4)),
        };

        var cost = SchedulerCost.Compute(placements, Input([Level("2025-09-01", "2025-09-28")]));

        cost.Should().Be(SchedulerCost.SpreadWeight * (SchedulerCost.MinGapDays - 3));
    }

    [Fact]
    public void LegGapPenalty_PenalisesDeviationFromHalfSeason()
    {
        var div = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var venueA = Guid.NewGuid();
        var venueB = Guid.NewGuid();

        // The two legs of A v B are 7 days apart; the season spans Sep 1–28 → target gap 13.
        var placements = new[]
        {
            new ScheduledMatch(div, a, b, venueA, new DateOnly(2025, 9, 1)),
            new ScheduledMatch(div, b, a, venueB, new DateOnly(2025, 9, 8)),
        };

        var cost = SchedulerCost.Compute(placements, Input([Level("2025-09-01", "2025-09-28")]));

        // Both teams have a 7-day spread (no spread penalty); only the leg-gap penalty applies.
        cost.Should().Be(SchedulerCost.LegWeight * (13 - 7));
    }
}
