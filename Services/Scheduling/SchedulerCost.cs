namespace smash_dates.Services.Scheduling;

// Soft-penalty cost of a schedule (lower is better). Two components:
//  - spread: closely-spaced matches for any one team are penalised;
//  - leg gap: the two legs of a pairing should be ~half a season apart.
// Default weights; per-League configuration is deferred (see ADR 0001).
public static class SchedulerCost
{
    public const int SpreadWeight = 2;
    public const int LegWeight = 1;
    public const int MinGapDays = 7;

    public static int Compute(IReadOnlyList<ScheduledMatch> matches, SchedulerInput input)
    {
        return SpreadPenalty(matches) + LegPenalty(matches, TargetGap(input));
    }

    private static int SpreadPenalty(IReadOnlyList<ScheduledMatch> matches)
    {
        var datesByTeam = new Dictionary<Guid, List<DateOnly>>();
        foreach (var m in matches)
        {
            (datesByTeam.TryGetValue(m.HomeTeamId, out var h) ? h : datesByTeam[m.HomeTeamId] = []).Add(m.Date);
            (datesByTeam.TryGetValue(m.AwayTeamId, out var a) ? a : datesByTeam[m.AwayTeamId] = []).Add(m.Date);
        }

        var penalty = 0;
        foreach (var dates in datesByTeam.Values)
        {
            dates.Sort();
            for (var i = 1; i < dates.Count; i++)
            {
                var gap = dates[i].DayNumber - dates[i - 1].DayNumber;
                if (gap < MinGapDays) penalty += SpreadWeight * (MinGapDays - gap);
            }
        }
        return penalty;
    }

    private static int LegPenalty(IReadOnlyList<ScheduledMatch> matches, int targetGap)
    {
        // Group the two legs of each pairing within a division (unordered team pair).
        var legs = new Dictionary<(Guid Division, Guid T1, Guid T2), List<DateOnly>>();
        foreach (var m in matches)
        {
            var key = m.HomeTeamId.CompareTo(m.AwayTeamId) < 0
                ? (m.DivisionId, m.HomeTeamId, m.AwayTeamId)
                : (m.DivisionId, m.AwayTeamId, m.HomeTeamId);
            (legs.TryGetValue(key, out var l) ? l : legs[key] = []).Add(m.Date);
        }

        var penalty = 0;
        foreach (var dates in legs.Values)
        {
            if (dates.Count < 2) continue;
            dates.Sort();
            var gap = dates[^1].DayNumber - dates[0].DayNumber;
            penalty += LegWeight * Math.Abs(gap - targetGap);
        }
        return penalty;
    }

    private static int TargetGap(SchedulerInput input)
    {
        if (input.Weeks.Count == 0) return 0;
        var min = input.Weeks.Min(w => w.Start);
        var max = input.Weeks.Max(w => w.End);
        return (max.DayNumber - min.DayNumber) / 2;
    }
}
