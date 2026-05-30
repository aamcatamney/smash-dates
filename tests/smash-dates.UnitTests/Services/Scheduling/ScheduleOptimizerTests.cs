using smash_dates.Models;
using smash_dates.Services.Scheduling;

namespace smash_dates.UnitTests.Services.Scheduling;

public sealed class ScheduleOptimizerTests
{
    private static SchedulerWeek Level(string date) =>
        new(DateOnly.Parse(date), DateOnly.Parse(date), WeekType.Level);

    [Fact]
    public void Optimize_SpreadsClusteredMatches_AndStaysFeasible()
    {
        // Six single-team clubs, one division, four single-day weeks.
        var clubs = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToList();
        var teams = clubs.Select(c => new SchedulerTeam(Guid.NewGuid(), c)).ToList();
        var venues = clubs.Select(c => new SchedulerVenue(Guid.NewGuid(), c, 1)).ToList();
        var div = new SchedulerDivision(Guid.NewGuid(), DivisionGender.Mens, teams);

        var d1 = new DateOnly(2025, 9, 1);
        var d2 = new DateOnly(2025, 9, 2);
        var d15 = new DateOnly(2025, 9, 15);
        var d16 = new DateOnly(2025, 9, 16);
        var input = new SchedulerInput([div], [Level("2025-09-01"), Level("2025-09-02"), Level("2025-09-15"), Level("2025-09-16")], venues, []);

        Guid T(int i) => teams[i].Id;
        Guid V(int i) => venues[i].Id;

        // Clustered start: team 0 plays Sep 1 & 2; team 3 plays Sep 15 & 16.
        var start = new[]
        {
            new ScheduledMatch(div.Id, T(0), T(1), V(0), d1),
            new ScheduledMatch(div.Id, T(0), T(2), V(0), d2),
            new ScheduledMatch(div.Id, T(3), T(4), V(3), d15),
            new ScheduledMatch(div.Id, T(3), T(5), V(3), d16),
        };

        SchedulerHardConstraints.IsFeasible(start, input).Should().BeTrue();
        var startCost = SchedulerCost.Compute(start, input);
        startCost.Should().BeGreaterThan(0);

        var optimized = ScheduleOptimizer.Optimize(start, input);

        SchedulerHardConstraints.IsFeasible(optimized, input).Should().BeTrue();
        SchedulerCost.Compute(optimized, input).Should().BeLessThan(startCost);
        optimized.Should().HaveCount(4); // same set of matches, only dates moved
    }

    [Fact]
    public void Optimize_AlreadyOptimal_LeavesCostUnchanged()
    {
        var clubs = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToList();
        var teams = clubs.Select(c => new SchedulerTeam(Guid.NewGuid(), c)).ToList();
        var venues = clubs.Select(c => new SchedulerVenue(Guid.NewGuid(), c, 1)).ToList();
        var div = new SchedulerDivision(Guid.NewGuid(), DivisionGender.Mens, teams);
        var input = new SchedulerInput([div], [Level("2025-09-01"), Level("2025-09-02"), Level("2025-09-15"), Level("2025-09-16")], venues, []);

        Guid T(int i) => teams[i].Id;
        Guid V(int i) => venues[i].Id;

        // Already well-spread: each team's two matches are 14 days apart.
        var start = new[]
        {
            new ScheduledMatch(div.Id, T(0), T(1), V(0), new DateOnly(2025, 9, 1)),
            new ScheduledMatch(div.Id, T(3), T(4), V(3), new DateOnly(2025, 9, 2)),
            new ScheduledMatch(div.Id, T(0), T(2), V(0), new DateOnly(2025, 9, 15)),
            new ScheduledMatch(div.Id, T(3), T(5), V(3), new DateOnly(2025, 9, 16)),
        };

        var before = SchedulerCost.Compute(start, input);
        var optimized = ScheduleOptimizer.Optimize(start, input);

        SchedulerCost.Compute(optimized, input).Should().BeLessThanOrEqualTo(before);
        SchedulerHardConstraints.IsFeasible(optimized, input).Should().BeTrue();
    }
}
