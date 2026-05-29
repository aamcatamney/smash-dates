namespace smash_dates.Services.Scheduling;

// Canonical double round-robin pairing via the circle method (Berger tables).
// First leg: each unordered pair meets once, grouped into rounds where every team
// plays at most once per round. Second leg: the same fixtures with home/away swapped.
public static class RoundRobin
{
    private static readonly Guid Bye = Guid.Empty;

    public static IReadOnlyList<(Guid Home, Guid Away)> DoubleRoundRobin(IReadOnlyList<Guid> teams)
    {
        if (teams.Count < 2) return [];

        var firstLeg = SingleRoundRobin(teams);
        var result = new List<(Guid Home, Guid Away)>(firstLeg.Count * 2);
        result.AddRange(firstLeg);
        result.AddRange(firstLeg.Select(p => (p.Away, p.Home)));
        return result;
    }

    private static List<(Guid Home, Guid Away)> SingleRoundRobin(IReadOnlyList<Guid> teams)
    {
        var arr = teams.ToList();
        if (arr.Count % 2 != 0) arr.Add(Bye); // odd team count → one team sits out each round

        var n = arr.Count;
        var fixtures = new List<(Guid Home, Guid Away)>();

        for (var round = 0; round < n - 1; round++)
        {
            for (var i = 0; i < n / 2; i++)
            {
                var t1 = arr[i];
                var t2 = arr[n - 1 - i];
                if (t1 == Bye || t2 == Bye) continue;

                // Alternate home/away so each team's home games are roughly balanced.
                fixtures.Add((round + i) % 2 == 0 ? (t1, t2) : (t2, t1));
            }

            // Rotate all but the first element one step clockwise.
            var last = arr[n - 1];
            for (var i = n - 1; i > 1; i--) arr[i] = arr[i - 1];
            arr[1] = last;
        }

        return fixtures;
    }
}
