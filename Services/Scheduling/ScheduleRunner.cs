using smash_dates.Models;
using smash_dates.Repositories;

namespace smash_dates.Services.Scheduling;

// Processes seasons sitting in the Scheduling state: runs the generator (which, on success,
// atomically persists the Proposed matches and moves the season to Proposed) or, on failure,
// records the reason and falls the season back to Draft. Scoped (scoped repositories); a fresh
// scope is created per tick by ScheduleRunnerHostedService. Durable: a season left Scheduling
// by a restart is picked up again next tick.
public sealed class ScheduleRunner
{
    private readonly ISeasonRepository _seasons;
    private readonly IScheduleGenerator _generator;

    public ScheduleRunner(ISeasonRepository seasons, IScheduleGenerator generator)
    {
        _seasons = seasons;
        _generator = generator;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        foreach (var season in await _seasons.ListByStatusAsync(SeasonStatus.Scheduling, ct))
        {
            try
            {
                var result = await _generator.GenerateAsync(season.Id, ct);
                if (!result.Success)
                    await _seasons.FailSchedulingAsync(
                        season.Id,
                        $"Could not place {result.Unplaced.Count} match(es) under the hard constraints. Adjust weeks, venues or blocked dates and try again.",
                        ct);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                await _seasons.FailSchedulingAsync(season.Id, "Schedule generation failed unexpectedly. Please try again.", ct);
            }
        }
    }
}
