using smash_dates.Repositories;

namespace smash_dates.Services.Scheduling;

public interface IScheduleGenerator
{
    // Gathers the season's scheduling inputs, runs the scheduler, and — only on full
    // success — persists the Proposed matches and moves the season to Proposed.
    Task<ScheduleResult> GenerateAsync(Guid seasonId, CancellationToken ct = default);

    // Incremental re-run: locks the season's Confirmed matches and re-places the rest.
    // On full success replaces the Proposed + Rejected matches (Confirmed untouched).
    Task<ScheduleResult> RerunAsync(Guid seasonId, CancellationToken ct = default);
}

public sealed class ScheduleGenerator : IScheduleGenerator
{
    private readonly IScheduler _scheduler;
    private readonly ISeasonRepository _seasons;
    private readonly ISeasonEntryRepository _entries;
    private readonly IVenueRepository _venues;
    private readonly IBlockedDateRepository _blockedDates;
    private readonly IMatchRepository _matches;
    private readonly ILeagueRepository _leagues;

    public ScheduleGenerator(
        IScheduler scheduler,
        ISeasonRepository seasons,
        ISeasonEntryRepository entries,
        IVenueRepository venues,
        IBlockedDateRepository blockedDates,
        IMatchRepository matches,
        ILeagueRepository leagues)
    {
        _scheduler = scheduler;
        _seasons = seasons;
        _entries = entries;
        _venues = venues;
        _blockedDates = blockedDates;
        _matches = matches;
        _leagues = leagues;
    }

    public async Task<ScheduleResult> GenerateAsync(Guid seasonId, CancellationToken ct = default)
    {
        var input = await BuildInputAsync(seasonId, locked: [], ct);
        var result = _scheduler.Build(input);

        if (result.Success)
            await _matches.InsertScheduleAsync(seasonId, result.Matches, ct);

        return result;
    }

    public async Task<ScheduleResult> RerunAsync(Guid seasonId, CancellationToken ct = default)
    {
        var locked = (await _matches.ListBySeasonAsync(seasonId, ct))
            .Where(m => m.Status == Models.MatchStatus.Confirmed)
            .Select(m => new LockedMatch(m.DivisionId, m.HomeTeamId, m.AwayTeamId, m.VenueId, m.MatchDate))
            .ToList();

        var input = await BuildInputAsync(seasonId, locked, ct);
        var result = _scheduler.Build(input);

        if (result.Success)
            await _matches.ReplaceProposedAndRejectedAsync(seasonId, result.Matches, ct);

        return result;
    }

    private async Task<SchedulerInput> BuildInputAsync(Guid seasonId, IReadOnlyList<LockedMatch> locked, CancellationToken ct)
    {
        var entries = await _entries.ListForSchedulingAsync(seasonId, ct);

        var divisions = entries
            .GroupBy(e => (e.DivisionId, e.Gender))
            .Select(g => new SchedulerDivision(
                g.Key.DivisionId,
                g.Key.Gender,
                g.Select(e => new SchedulerTeam(e.TeamId, e.ClubId)).ToList()))
            .ToList();

        var clubIds = entries.Select(e => e.ClubId).Distinct().ToList();

        // The League's courts-per-match rule turns each Venue's physical courts + its own
        // max-concurrent cap into the number of Matches a slot can hold.
        var weights = SchedulerWeights.Default;
        var courtsPerMatch = 2;
        var season = await _seasons.GetByIdAsync(seasonId, ct);
        if (season is not null && await _leagues.GetByIdAsync(season.LeagueId, ct) is { } league)
        {
            weights = new SchedulerWeights(league.SpreadWeight, league.LegWeight, league.MinGapDays, league.TargetGapDays);
            courtsPerMatch = league.CourtsPerMatch;
        }

        var venues = new List<SchedulerVenue>();
        var blocks = new List<SchedulerBlock>();
        foreach (var clubId in clubIds)
        {
            foreach (var v in await _venues.ListByClubAsync(clubId, ct))
                venues.Add(new SchedulerVenue(
                    v.Id, v.ClubId, VenueSlotCapacity.Compute(v.Courts, v.MaxConcurrentMatches, courtsPerMatch)));

            foreach (var b in await _blockedDates.ListByClubAsync(clubId, ct))
                blocks.Add(new SchedulerBlock(b.Scope, b.ClubId, b.VenueId, b.TeamId, b.StartDate, b.EndDate));
        }

        var weeks = (await _seasons.ListWeeksAsync(seasonId, ct))
            .Select(w => new SchedulerWeek(w.StartDate, w.EndDate, w.WeekType))
            .ToList();

        return new SchedulerInput(divisions, weeks, venues, blocks) { Locked = locked, Weights = weights };
    }
}
