using smash_dates.Models;
using smash_dates.Services.Scheduling;

namespace smash_dates.UnitTests.Services.Scheduling;

public sealed class SchedulerWeightsTests
{
    private static SchedulerWeek Level(string start, string end) =>
        new(DateOnly.Parse(start), DateOnly.Parse(end), WeekType.Level);

    private static SchedulerInput Input(SchedulerWeights weights, IReadOnlyList<SchedulerWeek> weeks) =>
        new([], weeks, [], []) { Weights = weights };

    [Fact]
    public void CustomSpreadWeight_ScalesTheSpreadPenalty()
    {
        var div = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var venue = Guid.NewGuid();
        var placements = new[]
        {
            new ScheduledMatch(div, a, b, venue, new DateOnly(2025, 9, 1)),
            new ScheduledMatch(div, a, c, venue, new DateOnly(2025, 9, 4)), // team A: 3-day gap
        };

        var cost = SchedulerCost.Compute(placements, Input(new SchedulerWeights(5, 1, 7, null), [Level("2025-09-01", "2025-09-28")]));

        cost.Should().Be(5 * (7 - 3)); // SpreadWeight * (MinGap - gap)
    }

    [Fact]
    public void CustomMinGapDays_BelowGap_NoSpreadPenalty()
    {
        var div = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var venue = Guid.NewGuid();
        var placements = new[]
        {
            new ScheduledMatch(div, a, b, venue, new DateOnly(2025, 9, 1)),
            new ScheduledMatch(div, a, c, venue, new DateOnly(2025, 9, 4)), // 3-day gap
        };

        // MinGap of 2 → a 3-day gap is fine → no spread penalty.
        var cost = SchedulerCost.Compute(placements, Input(new SchedulerWeights(5, 1, 2, null), [Level("2025-09-01", "2025-09-28")]));

        cost.Should().Be(0);
    }

    [Fact]
    public void TargetGapOverride_ReplacesHalfSeasonDefault()
    {
        var div = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var venueA = Guid.NewGuid();
        var venueB = Guid.NewGuid();
        var placements = new[]
        {
            new ScheduledMatch(div, a, b, venueA, new DateOnly(2025, 9, 1)),
            new ScheduledMatch(div, b, a, venueB, new DateOnly(2025, 9, 8)), // legs 7 days apart
        };

        // Override target to 7 → leg gap matches exactly → zero leg penalty (and 7-day spread = 0).
        var cost = SchedulerCost.Compute(placements, Input(new SchedulerWeights(2, 1, 7, 7), [Level("2025-09-01", "2025-09-28")]));

        cost.Should().Be(0);
    }
}
