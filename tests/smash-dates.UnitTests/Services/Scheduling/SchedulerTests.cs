using smash_dates.Models;
using smash_dates.Services.Scheduling;

namespace smash_dates.UnitTests.Services.Scheduling;

public sealed class SchedulerTests
{
    private static readonly IScheduler Scheduler = new Scheduler();

    private static SchedulerWeek Week(string start, string end, WeekType type) =>
        new(DateOnly.Parse(start), DateOnly.Parse(end), type);

    private static SchedulerBlock TeamBlock(Guid clubId, Guid teamId, string start, string end) =>
        new(BlockedDateScope.Team, clubId, null, teamId, DateOnly.Parse(start), DateOnly.Parse(end));

    private static SchedulerBlock VenueBlock(Guid clubId, Guid venueId, string start, string end) =>
        new(BlockedDateScope.Venue, clubId, venueId, null, DateOnly.Parse(start), DateOnly.Parse(end));

    [Fact]
    public void TwoTeams_Feasible_PlacesHomeAndAwayAtHomeVenues()
    {
        var clubA = Guid.NewGuid();
        var clubB = Guid.NewGuid();
        var t1 = new SchedulerTeam(Guid.NewGuid(), clubA);
        var t2 = new SchedulerTeam(Guid.NewGuid(), clubB);
        var venueA = new SchedulerVenue(Guid.NewGuid(), clubA, 1);
        var venueB = new SchedulerVenue(Guid.NewGuid(), clubB, 1);
        var div = new SchedulerDivision(Guid.NewGuid(), DivisionGender.Mens, [t1, t2]);
        var input = new SchedulerInput(
            [div],
            [Week("2025-09-01", "2025-09-07", WeekType.Level), Week("2025-09-08", "2025-09-14", WeekType.Level)],
            [venueA, venueB],
            []);

        var r = Scheduler.Build(input);

        r.Success.Should().BeTrue();
        r.Matches.Count.Should().Be(2);
        r.Matches.Should().Contain(m => m.HomeTeamId == t1.Id && m.AwayTeamId == t2.Id && m.VenueId == venueA.Id);
        r.Matches.Should().Contain(m => m.HomeTeamId == t2.Id && m.AwayTeamId == t1.Id && m.VenueId == venueB.Id);
        r.Matches.Select(m => m.Date).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void MixedDivision_OnlyPlacesInMixedWeeks()
    {
        var clubA = Guid.NewGuid();
        var clubB = Guid.NewGuid();
        var t1 = new SchedulerTeam(Guid.NewGuid(), clubA);
        var t2 = new SchedulerTeam(Guid.NewGuid(), clubB);
        var div = new SchedulerDivision(Guid.NewGuid(), DivisionGender.Mixed, [t1, t2]);
        var mixedWeek = Week("2025-09-08", "2025-09-14", WeekType.Mixed);
        var input = new SchedulerInput(
            [div],
            [Week("2025-09-01", "2025-09-07", WeekType.Level), mixedWeek],
            [new SchedulerVenue(Guid.NewGuid(), clubA, 1), new SchedulerVenue(Guid.NewGuid(), clubB, 1)],
            []);

        var r = Scheduler.Build(input);

        r.Success.Should().BeTrue();
        r.Matches.Should().OnlyContain(m => m.Date >= mixedWeek.Start && m.Date <= mixedWeek.End);
    }

    [Fact]
    public void MensDivision_NoLevelWeeks_IsInfeasible()
    {
        var clubA = Guid.NewGuid();
        var clubB = Guid.NewGuid();
        var t1 = new SchedulerTeam(Guid.NewGuid(), clubA);
        var t2 = new SchedulerTeam(Guid.NewGuid(), clubB);
        var div = new SchedulerDivision(Guid.NewGuid(), DivisionGender.Mens, [t1, t2]);
        var input = new SchedulerInput(
            [div],
            [Week("2025-09-08", "2025-09-14", WeekType.Mixed)], // only mixed weeks
            [new SchedulerVenue(Guid.NewGuid(), clubA, 1), new SchedulerVenue(Guid.NewGuid(), clubB, 1)],
            []);

        var r = Scheduler.Build(input);

        r.Success.Should().BeFalse();
        r.Unplaced.Should().HaveCount(2);
    }

    [Fact]
    public void TeamBlockedDates_AreAvoided()
    {
        var clubA = Guid.NewGuid();
        var clubB = Guid.NewGuid();
        var t1 = new SchedulerTeam(Guid.NewGuid(), clubA);
        var t2 = new SchedulerTeam(Guid.NewGuid(), clubB);
        var div = new SchedulerDivision(Guid.NewGuid(), DivisionGender.Mens, [t1, t2]);
        // One single-day week each; block t1 on the first → both legs must use the second.
        var input = new SchedulerInput(
            [div],
            [Week("2025-09-01", "2025-09-01", WeekType.Level), Week("2025-09-08", "2025-09-08", WeekType.Level)],
            [new SchedulerVenue(Guid.NewGuid(), clubA, 1), new SchedulerVenue(Guid.NewGuid(), clubB, 1)],
            [TeamBlock(clubA, t1.Id, "2025-09-01", "2025-09-01")]);

        var r = Scheduler.Build(input);

        // t1 plays in both legs, so neither can fall on the blocked date; only one free
        // date remains → at most one of the two legs is placeable.
        r.Matches.Should().OnlyContain(m => m.Date != new DateOnly(2025, 9, 1));
    }

    [Fact]
    public void VenueCapacityOne_PushesSecondMatchToAnotherDate()
    {
        // Two home matches for clubA's teams on the only club-A venue (capacity 1) must
        // land on different dates.
        var clubA = Guid.NewGuid();
        var clubB = Guid.NewGuid();
        var clubC = Guid.NewGuid();
        var a1 = new SchedulerTeam(Guid.NewGuid(), clubA);
        var b1 = new SchedulerTeam(Guid.NewGuid(), clubB);
        var c1 = new SchedulerTeam(Guid.NewGuid(), clubC);
        var venueA = new SchedulerVenue(Guid.NewGuid(), clubA, 1);
        var div = new SchedulerDivision(Guid.NewGuid(), DivisionGender.Mens, [a1, b1, c1]);
        var input = new SchedulerInput(
            [div],
            [
                Week("2025-09-01", "2025-09-07", WeekType.Level),
                Week("2025-09-08", "2025-09-14", WeekType.Level),
                Week("2025-09-15", "2025-09-21", WeekType.Level),
                Week("2025-09-22", "2025-09-28", WeekType.Level),
            ],
            [venueA, new SchedulerVenue(Guid.NewGuid(), clubB, 1), new SchedulerVenue(Guid.NewGuid(), clubC, 1)],
            []);

        var r = Scheduler.Build(input);

        r.Success.Should().BeTrue();
        var homeAtA = r.Matches.Where(m => m.VenueId == venueA.Id).Select(m => m.Date).ToList();
        homeAtA.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void NoTeamPlaysTwiceOnSameDate()
    {
        var clubs = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToList();
        var teams = clubs.Select(c => new SchedulerTeam(Guid.NewGuid(), c)).ToList();
        var venues = clubs.Select(c => new SchedulerVenue(Guid.NewGuid(), c, 2)).ToList();
        var div = new SchedulerDivision(Guid.NewGuid(), DivisionGender.Mens, teams);
        var weeks = Enumerable.Range(0, 12)
            .Select(i => Week($"2025-09-{1 + i * 2:00}", $"2025-09-{2 + i * 2:00}", WeekType.Level))
            .Take(6)
            .ToList();
        var input = new SchedulerInput([div], weeks, venues, []);

        var r = Scheduler.Build(input);

        r.Success.Should().BeTrue();
        foreach (var byDate in r.Matches.GroupBy(m => m.Date))
        {
            var teamsThatDate = byDate.SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId }).ToList();
            teamsThatDate.Should().OnlyHaveUniqueItems();
        }
    }

    [Fact]
    public void DerbyMatches_ScheduledBeforeTeamsPlayOthers()
    {
        // clubA has two teams in the division → their two intra-club (derby) matches must
        // be dated no later than either team's matches against the outsider.
        var clubA = Guid.NewGuid();
        var clubB = Guid.NewGuid();
        var a1 = new SchedulerTeam(Guid.NewGuid(), clubA);
        var a2 = new SchedulerTeam(Guid.NewGuid(), clubA);
        var b1 = new SchedulerTeam(Guid.NewGuid(), clubB);
        var div = new SchedulerDivision(Guid.NewGuid(), DivisionGender.Mens, [a1, a2, b1]);
        var weeks = Enumerable.Range(0, 8)
            .Select(i => Week($"2025-{9 + i / 4:00}-{1 + (i % 4) * 7:00}", $"2025-{9 + i / 4:00}-{1 + (i % 4) * 7:00}", WeekType.Level))
            .ToList();
        var input = new SchedulerInput(
            [div],
            weeks,
            [new SchedulerVenue(Guid.NewGuid(), clubA, 2), new SchedulerVenue(Guid.NewGuid(), clubB, 1)],
            []);

        var r = Scheduler.Build(input);

        r.Success.Should().BeTrue();
        var derbyDates = r.Matches
            .Where(m => (m.HomeTeamId == a1.Id || m.HomeTeamId == a2.Id) && (m.AwayTeamId == a1.Id || m.AwayTeamId == a2.Id))
            .Select(m => m.Date).ToList();
        var nonDerbyInvolvingA = r.Matches
            .Where(m => m.HomeTeamId == b1.Id || m.AwayTeamId == b1.Id)
            .Select(m => m.Date).ToList();
        derbyDates.Should().HaveCount(2);
        derbyDates.Max().Should().BeOnOrBefore(nonDerbyInvolvingA.Min());
    }
}
