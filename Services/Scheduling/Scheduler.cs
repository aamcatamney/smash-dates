using smash_dates.Models;

namespace smash_dates.Services.Scheduling;

// Custom heuristic scheduler (ADR 0001), hard-constraints-only first cut: Berger
// double round-robin per division, derby-first ordering, greedy earliest-slot placement.
// Soft-penalty local search is a later slice.
public sealed class Scheduler : IScheduler
{
    public ScheduleResult Build(SchedulerInput input)
    {
        var placed = new List<ScheduledMatch>();
        var unplaced = new List<UnplacedPairing>();

        // Mutable placement state, shared across divisions.
        var teamDates = new HashSet<(Guid Team, DateOnly Date)>();
        var slotUsage = new Dictionary<(Guid Venue, DateOnly Date), int>();

        var venuesByClub = input.Venues
            .GroupBy(v => v.ClubId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Seed occupancy from already-Confirmed (locked) fixtures so re-placed matches
        // don't collide with them. Locked pairings are not re-emitted.
        var lockedPairings = new HashSet<(Guid Division, Guid Home, Guid Away)>();
        foreach (var l in input.Locked)
        {
            teamDates.Add((l.HomeTeamId, l.Date));
            teamDates.Add((l.AwayTeamId, l.Date));
            slotUsage.TryGetValue((l.VenueId, l.Date), out var used);
            slotUsage[(l.VenueId, l.Date)] = used + 1;
            lockedPairings.Add((l.DivisionId, l.HomeTeamId, l.AwayTeamId));
        }

        foreach (var division in input.Divisions)
        {
            var weekType = division.Gender == DivisionGender.Mixed ? WeekType.Mixed : WeekType.Level;
            var dates = CandidateDates(input.Weeks, weekType);
            var clubByTeam = division.Teams.ToDictionary(t => t.Id, t => t.ClubId);

            var pairs = RoundRobin.DoubleRoundRobin(division.Teams.Select(t => t.Id).ToList())
                .Where(p => !lockedPairings.Contains((division.Id, p.Home, p.Away)))
                .ToList();

            // Derby = both teams share a club. Place derbies first so they fall before any
            // outside fixture for those teams (derby-first hard constraint).
            var derbies = pairs.Where(p => clubByTeam[p.Home] == clubByTeam[p.Away]).ToList();
            var others = pairs.Where(p => clubByTeam[p.Home] != clubByTeam[p.Away]).ToList();

            // A locked derby still constrains its teams' outside fixtures to fall after it.
            var maxDerbyDate = new Dictionary<Guid, DateOnly>();
            foreach (var l in input.Locked.Where(l => l.DivisionId == division.Id
                && clubByTeam.TryGetValue(l.HomeTeamId, out var hc)
                && clubByTeam.TryGetValue(l.AwayTeamId, out var ac) && hc == ac))
            {
                Track(maxDerbyDate, l.HomeTeamId, l.Date);
                Track(maxDerbyDate, l.AwayTeamId, l.Date);
            }

            foreach (var (home, away) in derbies)
            {
                var date = TryPlace(division, home, away, clubByTeam, venuesByClub, input.Blocks, dates,
                    teamDates, slotUsage, earliestExclusive: null, out var venueId);
                if (date is null)
                {
                    unplaced.Add(new UnplacedPairing(division.Id, home, away));
                    continue;
                }
                placed.Add(new ScheduledMatch(division.Id, home, away, venueId, date.Value));
                Track(maxDerbyDate, home, date.Value);
                Track(maxDerbyDate, away, date.Value);
            }

            foreach (var (home, away) in others)
            {
                // Must fall strictly after either team's last derby (if any).
                DateOnly? earliest = null;
                if (maxDerbyDate.TryGetValue(home, out var hd)) earliest = hd;
                if (maxDerbyDate.TryGetValue(away, out var ad) && (earliest is null || ad > earliest))
                    earliest = ad;

                var date = TryPlace(division, home, away, clubByTeam, venuesByClub, input.Blocks, dates,
                    teamDates, slotUsage, earliestExclusive: earliest, out var venueId);
                if (date is null)
                {
                    unplaced.Add(new UnplacedPairing(division.Id, home, away));
                    continue;
                }
                placed.Add(new ScheduledMatch(division.Id, home, away, venueId, date.Value));
            }
        }

        if (unplaced.Count > 0)
            return new ScheduleResult(false, placed, unplaced);

        // Improve the feasible schedule against the soft constraints (ADR 0001 phase 3).
        var optimised = ScheduleOptimizer.Optimize(placed, input);
        return new ScheduleResult(true, optimised, unplaced);
    }

    private static DateOnly? TryPlace(
        SchedulerDivision division,
        Guid home,
        Guid away,
        IReadOnlyDictionary<Guid, Guid> clubByTeam,
        IReadOnlyDictionary<Guid, List<SchedulerVenue>> venuesByClub,
        IReadOnlyList<SchedulerBlock> blocks,
        IReadOnlyList<DateOnly> dates,
        HashSet<(Guid, DateOnly)> teamDates,
        Dictionary<(Guid, DateOnly), int> slotUsage,
        DateOnly? earliestExclusive,
        out Guid venueId)
    {
        venueId = Guid.Empty;
        var homeClub = clubByTeam[home];
        var awayClub = clubByTeam[away];

        if (!venuesByClub.TryGetValue(homeClub, out var homeVenues)) return null;

        foreach (var date in dates)
        {
            if (earliestExclusive is { } min && date <= min) continue;
            if (teamDates.Contains((home, date)) || teamDates.Contains((away, date))) continue;
            if (IsTeamBlocked(blocks, home, date) || IsTeamBlocked(blocks, away, date)) continue;
            if (IsClubBlocked(blocks, homeClub, date) || IsClubBlocked(blocks, awayClub, date)) continue;

            foreach (var venue in homeVenues)
            {
                if (IsVenueBlocked(blocks, venue.Id, date)) continue;
                slotUsage.TryGetValue((venue.Id, date), out var used);
                if (used >= venue.Capacity) continue;

                slotUsage[(venue.Id, date)] = used + 1;
                teamDates.Add((home, date));
                teamDates.Add((away, date));
                venueId = venue.Id;
                return date;
            }
        }

        return null;
    }

    private static void Track(Dictionary<Guid, DateOnly> max, Guid team, DateOnly date)
    {
        if (!max.TryGetValue(team, out var current) || date > current) max[team] = date;
    }

    private static IReadOnlyList<DateOnly> CandidateDates(IReadOnlyList<SchedulerWeek> weeks, WeekType type)
    {
        var dates = new SortedSet<DateOnly>();
        foreach (var week in weeks.Where(w => w.Type == type))
        {
            for (var d = week.Start; d <= week.End; d = d.AddDays(1))
                dates.Add(d);
        }
        return dates.ToList();
    }

    private static bool IsTeamBlocked(IReadOnlyList<SchedulerBlock> blocks, Guid teamId, DateOnly date) =>
        blocks.Any(b => b.Scope == BlockedDateScope.Team && b.TeamId == teamId && InRange(b, date));

    private static bool IsClubBlocked(IReadOnlyList<SchedulerBlock> blocks, Guid clubId, DateOnly date) =>
        blocks.Any(b => b.Scope == BlockedDateScope.Club && b.ClubId == clubId && InRange(b, date));

    private static bool IsVenueBlocked(IReadOnlyList<SchedulerBlock> blocks, Guid venueId, DateOnly date) =>
        blocks.Any(b => b.Scope == BlockedDateScope.Venue && b.VenueId == venueId && InRange(b, date));

    private static bool InRange(SchedulerBlock b, DateOnly date) => date >= b.Start && date <= b.End;
}
