namespace smash_dates.Services.Standings;

public sealed record StandingTeam(Guid Id, string Name);

public sealed record StandingResult(Guid HomeTeamId, Guid AwayTeamId, int HomeScore, int AwayScore);

public sealed record PointsScheme(int Win, int Draw, int Loss);

public sealed record StandingRow(
    Guid TeamId,
    string TeamName,
    int Played,
    int Won,
    int Drawn,
    int Lost,
    int RubbersFor,
    int RubbersAgainst,
    int RubberDifference,
    int Points);

// Builds a league table for one (Season, Division) from played results. Pure and
// engine-agnostic: a walkover is just a maximal scoreline, so it needs no special case.
public static class StandingsCalculator
{
    public static IReadOnlyList<StandingRow> Compute(
        IReadOnlyList<StandingTeam> teams,
        PointsScheme scheme,
        IReadOnlyList<StandingResult> results)
    {
        var acc = teams.ToDictionary(t => t.Id, t => new Tally(t.Name));

        foreach (var r in results)
        {
            if (!acc.TryGetValue(r.HomeTeamId, out var home) || !acc.TryGetValue(r.AwayTeamId, out var away))
                continue; // result referencing a team not in this division — ignore defensively

            home.Apply(r.HomeScore, r.AwayScore, scheme);
            away.Apply(r.AwayScore, r.HomeScore, scheme);
        }

        var rows = acc.Select(kv => kv.Value.ToRow(kv.Key)).ToList();

        // Order by points → rubber difference → rubbers-for. Teams still level on all three
        // are split by their head-to-head record, with team name as the final stable fallback.
        return rows
            .GroupBy(r => (r.Points, r.RubberDifference, r.RubbersFor))
            .OrderByDescending(g => g.Key.Points)
            .ThenByDescending(g => g.Key.RubberDifference)
            .ThenByDescending(g => g.Key.RubbersFor)
            .SelectMany(g => OrderByHeadToHead([.. g], scheme, results))
            .ToList();
    }

    private static IEnumerable<StandingRow> OrderByHeadToHead(
        List<StandingRow> tied, PointsScheme scheme, IReadOnlyList<StandingResult> results)
    {
        if (tied.Count == 1) return tied;

        var ids = tied.Select(r => r.TeamId).ToHashSet();
        var h2h = tied.ToDictionary(r => r.TeamId, _ => (Points: 0, Diff: 0));

        foreach (var r in results)
        {
            if (!ids.Contains(r.HomeTeamId) || !ids.Contains(r.AwayTeamId)) continue;
            h2h[r.HomeTeamId] = Accumulate(h2h[r.HomeTeamId], r.HomeScore, r.AwayScore, scheme);
            h2h[r.AwayTeamId] = Accumulate(h2h[r.AwayTeamId], r.AwayScore, r.HomeScore, scheme);
        }

        return tied
            .OrderByDescending(r => h2h[r.TeamId].Points)
            .ThenByDescending(r => h2h[r.TeamId].Diff)
            .ThenBy(r => r.TeamName, StringComparer.OrdinalIgnoreCase);
    }

    private static (int Points, int Diff) Accumulate((int Points, int Diff) acc, int scored, int conceded, PointsScheme scheme)
    {
        var points = scored > conceded ? scheme.Win : scored == conceded ? scheme.Draw : scheme.Loss;
        return (acc.Points + points, acc.Diff + (scored - conceded));
    }

    private sealed class Tally(string name)
    {
        private int _played, _won, _drawn, _lost, _for, _against, _points;

        public void Apply(int scored, int conceded, PointsScheme scheme)
        {
            _played++;
            _for += scored;
            _against += conceded;

            if (scored > conceded) { _won++; _points += scheme.Win; }
            else if (scored == conceded) { _drawn++; _points += scheme.Draw; }
            else { _lost++; _points += scheme.Loss; }
        }

        public StandingRow ToRow(Guid teamId) =>
            new(teamId, name, _played, _won, _drawn, _lost, _for, _against, _for - _against, _points);
    }
}
