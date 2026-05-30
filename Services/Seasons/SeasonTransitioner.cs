using smash_dates.Models;
using smash_dates.Repositories;

namespace smash_dates.Services.Seasons;

// Advances seasons by the calendar (CONTEXT.md): Proposed -> Active once the first match
// date is reached; Active -> Closed once the end date has passed. `today` is passed in so
// the logic is deterministic and testable. Transitions are guarded (idempotent).
public sealed class SeasonTransitioner
{
    private readonly ISeasonRepository _seasons;
    private readonly IMatchRepository _matches;

    public SeasonTransitioner(ISeasonRepository seasons, IMatchRepository matches)
    {
        _seasons = seasons;
        _matches = matches;
    }

    public async Task RunAsync(DateOnly today, CancellationToken ct)
    {
        foreach (var season in await _seasons.ListByStatusAsync(SeasonStatus.Proposed, ct))
        {
            var earliest = await _matches.EarliestMatchDateAsync(season.Id, ct);
            if (earliest is { } first && today >= first)
                await _seasons.TransitionStatusAsync(season.Id, SeasonStatus.Proposed, SeasonStatus.Active, ct);
        }

        foreach (var season in await _seasons.ListByStatusAsync(SeasonStatus.Active, ct))
        {
            if (today > season.EndDate)
                await _seasons.TransitionStatusAsync(season.Id, SeasonStatus.Active, SeasonStatus.Closed, ct);
        }
    }
}
