using smash_dates.Models;

namespace smash_dates.Services.Pegboard;

/// Picks a valid lineup from the waiting queue for a game type, optimising fairness,
/// makeup, partner/opponent variety and grade balance. Pure and deterministic.
public static class PegboardFiller
{
    private const int PoolCap = 8;     // bound combinatorics: only consider the front of the queue
    private const int DefaultGrade = 3;

    public static FillSuggestion? Suggest(
        GameType type, IReadOnlyList<FillCandidate> waiting, IReadOnlyList<(Guid A, Guid B)> playedPairs)
    {
        var size = GameMakeup.SideSize(type);
        var need = size * 2;
        if (waiting.Count < need) return null;

        var pool = waiting.OrderBy(c => c.Order).Take(PoolCap).ToList();
        var pairSet = new HashSet<(Guid, Guid)>();
        foreach (var (a, b) in playedPairs) pairSet.Add(Key(a, b));

        FillSuggestion? best = null;
        double bestScore = double.MaxValue;

        // Enumerate combinations of `need` from the pool, then the best side split.
        foreach (var combo in Combinations(pool, need))
        {
            foreach (var (sideA, sideB) in SideSplits(combo, size))
            {
                var gendersA = sideA.Select(c => c.Gender).ToList();
                var gendersB = sideB.Select(c => c.Gender).ToList();
                if (!GameMakeup.IsValid(type, gendersA, gendersB)) continue;

                var score = Score(sideA, sideB, pairSet);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = new FillSuggestion(sideA.Select(c => c.Id).ToList(), sideB.Select(c => c.Id).ToList());
                }
            }
        }
        return best;
    }

    private static double Score(
        IReadOnlyList<FillCandidate> a, IReadOnlyList<FillCandidate> b, HashSet<(Guid, Guid)> playedPairs)
    {
        // Fairness: prefer players nearer the front of the queue (lower Order sum).
        var fairness = a.Concat(b).Sum(c => c.Order);

        // Variety: penalise every pair (same side or opposite) that already played together tonight.
        var all = a.Concat(b).ToList();
        var repeats = 0;
        for (var i = 0; i < all.Count; i++)
            for (var j = i + 1; j < all.Count; j++)
                if (playedPairs.Contains(Key(all[i].Id, all[j].Id))) repeats++;

        // Grade balance: even the two sides' grade sums (null = mid).
        var gradeImbalance = Math.Abs(a.Sum(c => c.Grade ?? DefaultGrade) - b.Sum(c => c.Grade ?? DefaultGrade));

        // Weights: fairness dominates, then variety, then grade balance.
        return fairness + repeats * 100 + gradeImbalance * 5;
    }

    private static (Guid, Guid) Key(Guid x, Guid y) => x.CompareTo(y) < 0 ? (x, y) : (y, x);

    private static IEnumerable<List<FillCandidate>> Combinations(IReadOnlyList<FillCandidate> items, int k)
    {
        var n = items.Count;
        var idx = Enumerable.Range(0, k).ToArray();
        while (true)
        {
            yield return idx.Select(i => items[i]).ToList();
            var p = k - 1;
            while (p >= 0 && idx[p] == n - k + p) p--;
            if (p < 0) yield break;
            idx[p]++;
            for (var i = p + 1; i < k; i++) idx[i] = idx[i - 1] + 1;
        }
    }

    // Split `combo` of size 2*sideSize into (A,B) of sideSize each. To avoid mirror duplicates,
    // fix the first element on side A.
    private static IEnumerable<(List<FillCandidate> A, List<FillCandidate> B)> SideSplits(
        List<FillCandidate> combo, int sideSize)
    {
        if (sideSize == 1)
        {
            yield return ([combo[0]], [combo[1]]);
            yield break;
        }
        var first = combo[0];
        var rest = combo.Skip(1).ToList();
        // choose (sideSize-1) partners for `first`; the remainder is side B
        foreach (var partnerIdx in Combinations(Enumerable.Range(0, rest.Count).Select(i => new FillCandidate(rest[i].Id, rest[i].Gender, rest[i].Grade, i)).ToList(), sideSize - 1))
        {
            var partnerOrders = partnerIdx.Select(c => c.Order).ToHashSet();
            var a = new List<FillCandidate> { first };
            var b = new List<FillCandidate>();
            for (var i = 0; i < rest.Count; i++)
                (partnerOrders.Contains(i) ? a : b).Add(rest[i]);
            yield return (a, b);
        }
    }
}
