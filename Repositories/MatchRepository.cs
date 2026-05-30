using Dapper;
using smash_dates.Data;
using smash_dates.Models;
using smash_dates.Services.Scheduling;

namespace smash_dates.Repositories;

public sealed class MatchRepository : IMatchRepository
{
    private const string ViewSelect =
        @"SELECT m.id, m.season_id, m.division_id, d.name AS division_name,
                 m.home_team_id, h.name AS home_team_name,
                 m.away_team_id, a.name AS away_team_name,
                 m.venue_id, v.name AS venue_name,
                 m.match_date, m.status, m.home_accepted, m.away_accepted,
                 m.home_score, m.away_score, m.played_on, m.is_walkover
          FROM matches m
          JOIN divisions d ON d.id = m.division_id
          JOIN teams h ON h.id = m.home_team_id
          JOIN teams a ON a.id = m.away_team_id
          JOIN venues v ON v.id = m.venue_id";

    private readonly IDbConnectionFactory _factory;

    public MatchRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<MatchView>> ListBySeasonAsync(Guid seasonId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<ViewRow>(
            new CommandDefinition(
                $"{ViewSelect} WHERE m.season_id = @seasonId ORDER BY m.match_date, d.rank, h.name",
                new { seasonId },
                cancellationToken: ct));
        return rows.Select(r => r.ToView()).ToList();
    }

    public async Task<IReadOnlyList<MatchView>> ListByClubAsync(Guid clubId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<ViewRow>(
            new CommandDefinition(
                $"{ViewSelect} WHERE h.club_id = @clubId OR a.club_id = @clubId ORDER BY m.match_date, d.rank, h.name",
                new { clubId },
                cancellationToken: ct));
        return rows.Select(r => r.ToView()).ToList();
    }

    public async Task<MatchView?> GetViewByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var row = await conn.QuerySingleOrDefaultAsync<ViewRow>(
            new CommandDefinition($"{ViewSelect} WHERE m.id = @id", new { id }, cancellationToken: ct));
        return row?.ToView();
    }

    public async Task<Match?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var row = await conn.QuerySingleOrDefaultAsync<MatchRow>(
            new CommandDefinition(
                @"SELECT id, season_id, division_id, home_team_id, away_team_id, venue_id,
                         match_date, status, home_accepted, away_accepted,
                         home_score, away_score, played_on, is_walkover, created_at
                  FROM matches WHERE id = @id",
                new { id },
                cancellationToken: ct));
        return row?.ToMatch();
    }

    public async Task InsertScheduleAsync(Guid seasonId, IReadOnlyList<ScheduledMatch> matches, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        if (conn is System.Data.Common.DbConnection dbConn)
            await dbConn.OpenAsync(ct);
        else
            conn.Open();

        using var tx = conn.BeginTransaction();

        foreach (var m in matches)
        {
            await conn.ExecuteAsync(
                new CommandDefinition(
                    @"INSERT INTO matches (season_id, division_id, home_team_id, away_team_id, venue_id, match_date)
                      VALUES (@seasonId, @divisionId, @homeTeamId, @awayTeamId, @venueId, @matchDate)",
                    new
                    {
                        seasonId,
                        divisionId = m.DivisionId,
                        homeTeamId = m.HomeTeamId,
                        awayTeamId = m.AwayTeamId,
                        venueId = m.VenueId,
                        matchDate = m.Date,
                    },
                    transaction: tx,
                    cancellationToken: ct));
        }

        await conn.ExecuteAsync(
            new CommandDefinition(
                "UPDATE seasons SET status = 'Proposed', updated_at = now() WHERE id = @seasonId",
                new { seasonId },
                transaction: tx,
                cancellationToken: ct));

        tx.Commit();
    }

    public async Task ReplaceProposedAndRejectedAsync(Guid seasonId, IReadOnlyList<ScheduledMatch> matches, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        if (conn is System.Data.Common.DbConnection dbConn)
            await dbConn.OpenAsync(ct);
        else
            conn.Open();

        using var tx = conn.BeginTransaction();

        await conn.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM matches WHERE season_id = @seasonId AND status IN ('Proposed', 'Rejected')",
                new { seasonId },
                transaction: tx,
                cancellationToken: ct));

        foreach (var m in matches)
        {
            await conn.ExecuteAsync(
                new CommandDefinition(
                    @"INSERT INTO matches (season_id, division_id, home_team_id, away_team_id, venue_id, match_date)
                      VALUES (@seasonId, @divisionId, @homeTeamId, @awayTeamId, @venueId, @matchDate)",
                    new
                    {
                        seasonId,
                        divisionId = m.DivisionId,
                        homeTeamId = m.HomeTeamId,
                        awayTeamId = m.AwayTeamId,
                        venueId = m.VenueId,
                        matchDate = m.Date,
                    },
                    transaction: tx,
                    cancellationToken: ct));
        }

        tx.Commit();
    }

    public async Task<bool> ExistsForVenueAsync(Guid venueId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT EXISTS(SELECT 1 FROM matches WHERE venue_id = @venueId)",
                new { venueId },
                cancellationToken: ct));
    }

    public async Task<DateOnly?> EarliestMatchDateAsync(Guid seasonId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<DateOnly?>(
            new CommandDefinition(
                "SELECT MIN(match_date) FROM matches WHERE season_id = @seasonId",
                new { seasonId },
                cancellationToken: ct));
    }

    public async Task<bool> ApplyAcceptAsync(Guid id, bool acceptHome, bool acceptAway, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE matches
                  SET home_accepted = home_accepted OR @acceptHome,
                      away_accepted = away_accepted OR @acceptAway,
                      status = CASE
                          WHEN (home_accepted OR @acceptHome) AND (away_accepted OR @acceptAway) THEN 'Confirmed'
                          ELSE status
                      END
                  WHERE id = @id AND status = 'Proposed'",
                new { id, acceptHome, acceptAway },
                cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> RejectAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                "UPDATE matches SET status = 'Rejected' WHERE id = @id AND status = 'Proposed'",
                new { id },
                cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> ForceConfirmAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE matches
                  SET status = 'Confirmed', home_accepted = true, away_accepted = true
                  WHERE id = @id AND status = 'Proposed'",
                new { id },
                cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> RecordResultAsync(Guid id, int homeScore, int awayScore, DateOnly playedOn, bool isWalkover, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE matches
                  SET status = 'Played', home_score = @homeScore, away_score = @awayScore,
                      played_on = @playedOn, is_walkover = @isWalkover
                  WHERE id = @id AND status = 'Confirmed'",
                new { id, homeScore, awayScore, playedOn, isWalkover },
                cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> PostponeAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE matches
                  SET status = 'Proposed', home_accepted = false, away_accepted = false
                  WHERE id = @id AND status = 'Confirmed'",
                new { id },
                cancellationToken: ct));
        return rows > 0;
    }

    private sealed class MatchRow
    {
        public Guid Id { get; init; }
        public Guid SeasonId { get; init; }
        public Guid DivisionId { get; init; }
        public Guid HomeTeamId { get; init; }
        public Guid AwayTeamId { get; init; }
        public Guid VenueId { get; init; }
        public DateOnly MatchDate { get; init; }
        public string Status { get; init; } = string.Empty;
        public bool HomeAccepted { get; init; }
        public bool AwayAccepted { get; init; }
        public int? HomeScore { get; init; }
        public int? AwayScore { get; init; }
        public DateOnly? PlayedOn { get; init; }
        public bool IsWalkover { get; init; }
        public DateTime CreatedAt { get; init; }

        public Match ToMatch() => new()
        {
            Id = Id,
            SeasonId = SeasonId,
            DivisionId = DivisionId,
            HomeTeamId = HomeTeamId,
            AwayTeamId = AwayTeamId,
            VenueId = VenueId,
            MatchDate = MatchDate,
            Status = Enum.Parse<MatchStatus>(Status),
            HomeAccepted = HomeAccepted,
            AwayAccepted = AwayAccepted,
            HomeScore = HomeScore,
            AwayScore = AwayScore,
            PlayedOn = PlayedOn,
            IsWalkover = IsWalkover,
            CreatedAt = CreatedAt,
        };
    }

    private sealed class ViewRow
    {
        public Guid Id { get; init; }
        public Guid SeasonId { get; init; }
        public Guid DivisionId { get; init; }
        public string DivisionName { get; init; } = string.Empty;
        public Guid HomeTeamId { get; init; }
        public string HomeTeamName { get; init; } = string.Empty;
        public Guid AwayTeamId { get; init; }
        public string AwayTeamName { get; init; } = string.Empty;
        public Guid VenueId { get; init; }
        public string VenueName { get; init; } = string.Empty;
        public DateOnly MatchDate { get; init; }
        public string Status { get; init; } = string.Empty;
        public bool HomeAccepted { get; init; }
        public bool AwayAccepted { get; init; }
        public int? HomeScore { get; init; }
        public int? AwayScore { get; init; }
        public DateOnly? PlayedOn { get; init; }
        public bool IsWalkover { get; init; }

        public MatchView ToView() => new()
        {
            Id = Id,
            SeasonId = SeasonId,
            DivisionId = DivisionId,
            DivisionName = DivisionName,
            HomeTeamId = HomeTeamId,
            HomeTeamName = HomeTeamName,
            AwayTeamId = AwayTeamId,
            AwayTeamName = AwayTeamName,
            VenueId = VenueId,
            VenueName = VenueName,
            MatchDate = MatchDate,
            Status = Enum.Parse<MatchStatus>(Status),
            HomeAccepted = HomeAccepted,
            AwayAccepted = AwayAccepted,
            HomeScore = HomeScore,
            AwayScore = AwayScore,
            PlayedOn = PlayedOn,
            IsWalkover = IsWalkover,
        };
    }
}
