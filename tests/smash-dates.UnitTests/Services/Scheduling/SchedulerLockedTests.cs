using smash_dates.Models;
using smash_dates.Services.Scheduling;

namespace smash_dates.UnitTests.Services.Scheduling;

public sealed class SchedulerLockedTests
{
    private static readonly IScheduler Scheduler = new Scheduler();

    private static SchedulerWeek Level(string date) =>
        new(DateOnly.Parse(date), DateOnly.Parse(date), WeekType.Level);

    [Fact]
    public void LockedMatch_IsExcluded_AndItsOccupancyConstrainsPlacement()
    {
        var clubA = Guid.NewGuid();
        var clubB = Guid.NewGuid();
        var t1 = new SchedulerTeam(Guid.NewGuid(), clubA);
        var t2 = new SchedulerTeam(Guid.NewGuid(), clubB);
        var venueA = new SchedulerVenue(Guid.NewGuid(), clubA, 1);
        var venueB = new SchedulerVenue(Guid.NewGuid(), clubB, 1);
        var div = new SchedulerDivision(Guid.NewGuid(), DivisionGender.Mens, [t1, t2]);
        var d1 = new DateOnly(2025, 9, 1);

        // The t1-home leg is already Confirmed on d1 at venueA.
        var input = new SchedulerInput(
            [div],
            [Level("2025-09-01"), Level("2025-09-08")],
            [venueA, venueB],
            [])
        {
            Locked = [new LockedMatch(div.Id, t1.Id, t2.Id, venueA.Id, d1)],
        };

        var r = Scheduler.Build(input);

        r.Success.Should().BeTrue();
        // Only the return leg (t2 home) is (re)placed; the locked leg is not re-emitted.
        r.Matches.Should().ContainSingle();
        r.Matches[0].HomeTeamId.Should().Be(t2.Id);
        r.Matches[0].AwayTeamId.Should().Be(t1.Id);
        // Both teams already play on d1 (the locked match), so the return leg can't reuse it.
        r.Matches[0].Date.Should().NotBe(d1);
    }

    [Fact]
    public void LockedMatch_LeavesNoRoom_IsInfeasible()
    {
        var clubA = Guid.NewGuid();
        var clubB = Guid.NewGuid();
        var t1 = new SchedulerTeam(Guid.NewGuid(), clubA);
        var t2 = new SchedulerTeam(Guid.NewGuid(), clubB);
        var venueA = new SchedulerVenue(Guid.NewGuid(), clubA, 1);
        var venueB = new SchedulerVenue(Guid.NewGuid(), clubB, 1);
        var div = new SchedulerDivision(Guid.NewGuid(), DivisionGender.Mens, [t1, t2]);
        var d1 = new DateOnly(2025, 9, 1);

        // Only one date exists and it is taken by the locked leg → return leg can't be placed.
        var input = new SchedulerInput(
            [div],
            [Level("2025-09-01")],
            [venueA, venueB],
            [])
        {
            Locked = [new LockedMatch(div.Id, t1.Id, t2.Id, venueA.Id, d1)],
        };

        var r = Scheduler.Build(input);

        r.Success.Should().BeFalse();
        r.Unplaced.Should().ContainSingle();
    }
}
