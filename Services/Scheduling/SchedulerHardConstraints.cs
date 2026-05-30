using smash_dates.Models;

namespace smash_dates.Services.Scheduling;

// Validates that a full set of placements satisfies every hard constraint. Used by the
// local-search optimiser to reject any move that would break feasibility.
public static class SchedulerHardConstraints
{
    public static bool IsFeasible(IReadOnlyList<ScheduledMatch> matches, SchedulerInput input)
    {
        var clubByTeam = new Dictionary<Guid, Guid>();
        var genderByDivision = new Dictionary<Guid, DivisionGender>();
        foreach (var d in input.Divisions)
        {
            genderByDivision[d.Id] = d.Gender;
            foreach (var t in d.Teams) clubByTeam[t.Id] = t.ClubId;
        }

        var venueById = input.Venues.ToDictionary(v => v.Id);
        var levelDates = DatesOfType(input.Weeks, WeekType.Level);
        var mixedDates = DatesOfType(input.Weeks, WeekType.Mixed);

        var teamDate = new HashSet<(Guid, DateOnly)>();
        var slotUsage = new Dictionary<(Guid, DateOnly), int>();

        foreach (var m in matches)
        {
            if (!clubByTeam.TryGetValue(m.HomeTeamId, out var homeClub)) return false;
            if (!clubByTeam.TryGetValue(m.AwayTeamId, out var awayClub)) return false;

            // Week-type ↔ division gender.
            var weekType = genderByDivision.TryGetValue(m.DivisionId, out var g) && g == DivisionGender.Mixed
                ? WeekType.Mixed : WeekType.Level;
            var validDates = weekType == WeekType.Mixed ? mixedDates : levelDates;
            if (!validDates.Contains(m.Date)) return false;

            // Home venue must belong to the home club and be free of venue blocks.
            if (!venueById.TryGetValue(m.VenueId, out var venue) || venue.ClubId != homeClub) return false;
            if (IsVenueBlocked(input.Blocks, m.VenueId, m.Date)) return false;

            // Blocked dates: neither club club-blocked, neither team team-blocked.
            if (IsClubBlocked(input.Blocks, homeClub, m.Date) || IsClubBlocked(input.Blocks, awayClub, m.Date)) return false;
            if (IsTeamBlocked(input.Blocks, m.HomeTeamId, m.Date) || IsTeamBlocked(input.Blocks, m.AwayTeamId, m.Date)) return false;

            // One match per team per date.
            if (!teamDate.Add((m.HomeTeamId, m.Date)) || !teamDate.Add((m.AwayTeamId, m.Date))) return false;

            // Venue court capacity.
            slotUsage.TryGetValue((m.VenueId, m.Date), out var used);
            if (used + 1 > venue.Capacity) return false;
            slotUsage[(m.VenueId, m.Date)] = used + 1;
        }

        return DerbyFirstSatisfied(matches, clubByTeam);
    }

    private static bool DerbyFirstSatisfied(IReadOnlyList<ScheduledMatch> matches, IReadOnlyDictionary<Guid, Guid> clubByTeam)
    {
        // Per division, every derby date for a team must be on or before all its non-derby dates.
        foreach (var byDivision in matches.GroupBy(m => m.DivisionId))
        {
            var maxDerby = new Dictionary<Guid, DateOnly>();
            var minOther = new Dictionary<Guid, DateOnly>();

            foreach (var m in byDivision)
            {
                var derby = clubByTeam[m.HomeTeamId] == clubByTeam[m.AwayTeamId];
                foreach (var team in new[] { m.HomeTeamId, m.AwayTeamId })
                {
                    if (derby)
                    {
                        if (!maxDerby.TryGetValue(team, out var cur) || m.Date > cur) maxDerby[team] = m.Date;
                    }
                    else
                    {
                        if (!minOther.TryGetValue(team, out var cur) || m.Date < cur) minOther[team] = m.Date;
                    }
                }
            }

            foreach (var (team, derbyDate) in maxDerby)
                if (minOther.TryGetValue(team, out var other) && derbyDate > other) return false;
        }
        return true;
    }

    private static HashSet<DateOnly> DatesOfType(IReadOnlyList<SchedulerWeek> weeks, WeekType type)
    {
        var set = new HashSet<DateOnly>();
        foreach (var w in weeks.Where(w => w.Type == type))
            for (var d = w.Start; d <= w.End; d = d.AddDays(1))
                set.Add(d);
        return set;
    }

    private static bool IsTeamBlocked(IReadOnlyList<SchedulerBlock> b, Guid teamId, DateOnly date) =>
        b.Any(x => x.Scope == BlockedDateScope.Team && x.TeamId == teamId && InRange(x, date));

    private static bool IsClubBlocked(IReadOnlyList<SchedulerBlock> b, Guid clubId, DateOnly date) =>
        b.Any(x => x.Scope == BlockedDateScope.Club && x.ClubId == clubId && InRange(x, date));

    private static bool IsVenueBlocked(IReadOnlyList<SchedulerBlock> b, Guid venueId, DateOnly date) =>
        b.Any(x => x.Scope == BlockedDateScope.Venue && x.VenueId == venueId && InRange(x, date));

    private static bool InRange(SchedulerBlock b, DateOnly date) => date >= b.Start && date <= b.End;
}
