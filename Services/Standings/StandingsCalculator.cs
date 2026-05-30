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

        return acc
            .Select(kv => kv.Value.ToRow(kv.Key))
            .OrderByDescending(r => r.Points)
            .ThenByDescending(r => r.RubberDifference)
            .ThenByDescending(r => r.RubbersFor)
            .ThenBy(r => r.TeamName, StringComparer.OrdinalIgnoreCase)
            .ToList();
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
