namespace smash_dates.Services.Scheduling;

// Deterministic 2-opt local search: repeatedly swap the dates of two placed matches
// (keeping each at its home venue) whenever the swap stays hard-feasible and lowers the
// soft-penalty cost. No RNG — order of evaluation is fixed, so results are reproducible.
public static class ScheduleOptimizer
{
    private const int MaxPasses = 8;

    public static IReadOnlyList<ScheduledMatch> Optimize(IReadOnlyList<ScheduledMatch> start, SchedulerInput input)
    {
        var current = start.ToList();
        var currentCost = SchedulerCost.Compute(current, input);

        for (var pass = 0; pass < MaxPasses; pass++)
        {
            var improved = false;

            for (var i = 0; i < current.Count; i++)
            {
                for (var j = i + 1; j < current.Count; j++)
                {
                    if (current[i].Date == current[j].Date) continue;

                    var candidate = current.ToList();
                    candidate[i] = current[i] with { Date = current[j].Date };
                    candidate[j] = current[j] with { Date = current[i].Date };

                    if (!SchedulerHardConstraints.IsFeasible(candidate, input)) continue;

                    var cost = SchedulerCost.Compute(candidate, input);
                    if (cost < currentCost)
                    {
                        current = candidate;
                        currentCost = cost;
                        improved = true;
                    }
                }
            }

            if (!improved) break;
        }

        return current;
    }
}
