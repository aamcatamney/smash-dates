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

    // Dry-run diagnostics: builds the same input and runs the scheduler, but persists nothing —
    // reports per-division feasibility and the pairings the scheduler couldn't place.
    Task<SchedulingDiagnostics> DiagnoseAsync(Guid seasonId, CancellationToken ct = default);
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
    private readonly IDivisionRepository _divisions;

    public ScheduleGenerator(
        IScheduler scheduler,
        ISeasonRepository seasons,
        ISeasonEntryRepository entries,
        IVenueRepository venues,
        IBlockedDateRepository blockedDates,
        IMatchRepository matches,
        ILeagueRepository leagues,
        IDivisionRepository divisions)
    {
        _scheduler = scheduler;
        _seasons = seasons;
        _entries = entries;
        _venues = venues;
        _blockedDates = blockedDates;
        _matches = matches;
        _leagues = leagues;
        _divisions = divisions;
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

    public async Task<SchedulingDiagnostics> DiagnoseAsync(Guid seasonId, CancellationToken ct = default)
    {
        var input = await BuildInputAsync(seasonId, locked: [], ct);
        var result = _scheduler.Build(input); // dry run — nothing is persisted

        var season = await _seasons.GetByIdAsync(seasonId, ct);
        var divisionNames = season is null
            ? new Dictionary<Guid, string>()
            : (await _divisions.ListByLeagueAsync(season.LeagueId, ct)).ToDictionary(d => d.Id, d => d.Name);
        var teamNames = (await _entries.ListBySeasonAsync(seasonId, ct))
            .GroupBy(e => e.TeamId)
            .ToDictionary(g => g.Key, g => g.First().TeamName);

        string DivName(Guid id) => divisionNames.GetValueOrDefault(id, id.ToString());
        string TeamName(Guid id) => teamNames.GetValueOrDefault(id, id.ToString());

        var placedByDivision = result.Matches
            .GroupBy(m => m.DivisionId)
            .ToDictionary(g => g.Key, g => g.Count());

        var divisions = input.Divisions
            .Select(d =>
            {
                var teams = d.Teams.Count;
                var eligibleWeeks = input.Weeks.Count(w => WeekTypeMatches(d.Gender, w.Type));
                return new DivisionDiagnostic(
                    d.Id, DivName(d.Id), teams,
                    MatchesRequired: teams * (teams - 1), // double round-robin
                    MatchesPlaced: placedByDivision.GetValueOrDefault(d.Id, 0),
                    EligibleWeeks: eligibleWeeks);
            })
            .ToList();

        var unplaced = result.Unplaced
            .Select(u => new UnplacedPairingInfo(u.DivisionId, DivName(u.DivisionId), TeamName(u.HomeTeamId), TeamName(u.AwayTeamId)))
            .ToList();

        return new SchedulingDiagnostics(
            FullyPlaced: result.Success && unplaced.Count == 0,
            TotalRequired: divisions.Sum(d => d.MatchesRequired),
            TotalPlaced: result.Matches.Count,
            Divisions: divisions,
            Unplaced: unplaced);
    }

    // Mens/Ladies divisions play in Level weeks; Mixed divisions play in Mixed weeks.
    private static bool WeekTypeMatches(Models.DivisionGender gender, Models.WeekType weekType) =>
        gender == Models.DivisionGender.Mixed ? weekType == Models.WeekType.Mixed : weekType == Models.WeekType.Level;

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
