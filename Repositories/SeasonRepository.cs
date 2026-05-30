using Dapper;
using smash_dates.Data;
using smash_dates.Models;

namespace smash_dates.Repositories;

public sealed class SeasonRepository : ISeasonRepository
{
    private const string SeasonColumns =
        "id, league_id, name, start_date, end_date, status, created_at, updated_at";

    private readonly IDbConnectionFactory _factory;

    public SeasonRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<Season?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var row = await conn.QuerySingleOrDefaultAsync<SeasonRow>(
            new CommandDefinition(
                $"SELECT {SeasonColumns} FROM seasons WHERE id = @id",
                new { id },
                cancellationToken: ct));
        return row?.ToSeason();
    }

    public async Task<IReadOnlyList<Season>> ListByLeagueAsync(Guid leagueId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<SeasonRow>(
            new CommandDefinition(
                $@"SELECT {SeasonColumns} FROM seasons
                   WHERE league_id = @leagueId
                   ORDER BY start_date DESC",
                new { leagueId },
                cancellationToken: ct));
        return rows.Select(r => r.ToSeason()).ToList();
    }

    public async Task<IReadOnlyList<SeasonWeek>> ListWeeksAsync(Guid seasonId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<WeekRow>(
            new CommandDefinition(
                @"SELECT id, season_id, start_date, end_date, week_type
                  FROM season_weeks
                  WHERE season_id = @seasonId
                  ORDER BY start_date",
                new { seasonId },
                cancellationToken: ct));
        return rows.Select(r => r.ToWeek()).ToList();
    }

    public async Task<Guid> CreateWithWeeksAsync(
        Guid leagueId,
        string name,
        DateOnly startDate,
        DateOnly endDate,
        IReadOnlyList<(DateOnly StartDate, DateOnly EndDate, WeekType WeekType)> weeks,
        CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        if (conn is System.Data.Common.DbConnection dbConn)
            await dbConn.OpenAsync(ct);
        else
            conn.Open();

        using var tx = conn.BeginTransaction();

        var id = await conn.ExecuteScalarAsync<Guid>(
            new CommandDefinition(
                @"INSERT INTO seasons (league_id, name, start_date, end_date)
                  VALUES (@leagueId, @name, @startDate, @endDate)
                  RETURNING id",
                new { leagueId, name, startDate, endDate },
                transaction: tx,
                cancellationToken: ct));

        await InsertWeeksAsync(conn, tx, id, weeks, ct);

        tx.Commit();
        return id;
    }

    public async Task ReplaceWeeksAsync(
        Guid seasonId,
        IReadOnlyList<(DateOnly StartDate, DateOnly EndDate, WeekType WeekType)> weeks,
        CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        if (conn is System.Data.Common.DbConnection dbConn)
            await dbConn.OpenAsync(ct);
        else
            conn.Open();

        using var tx = conn.BeginTransaction();

        await conn.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM season_weeks WHERE season_id = @seasonId",
                new { seasonId },
                transaction: tx,
                cancellationToken: ct));

        await InsertWeeksAsync(conn, tx, seasonId, weeks, ct);

        tx.Commit();
    }

    private static async Task InsertWeeksAsync(
        System.Data.IDbConnection conn,
        System.Data.IDbTransaction tx,
        Guid seasonId,
        IReadOnlyList<(DateOnly StartDate, DateOnly EndDate, WeekType WeekType)> weeks,
        CancellationToken ct)
    {
        foreach (var w in weeks)
        {
            await conn.ExecuteAsync(
                new CommandDefinition(
                    @"INSERT INTO season_weeks (season_id, start_date, end_date, week_type)
                      VALUES (@seasonId, @startDate, @endDate, @weekType)",
                    new { seasonId, startDate = w.StartDate, endDate = w.EndDate, weekType = w.WeekType.ToString() },
                    transaction: tx,
                    cancellationToken: ct));
        }
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM seasons WHERE id = @id",
                new { id },
                cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> TransitionStatusAsync(Guid id, SeasonStatus from, SeasonStatus to, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                "UPDATE seasons SET status = @to, updated_at = now() WHERE id = @id AND status = @from",
                new { id, from = from.ToString(), to = to.ToString() },
                cancellationToken: ct));
        return rows > 0;
    }

    private sealed class SeasonRow
    {
        public Guid Id { get; init; }
        public Guid LeagueId { get; init; }
        public string Name { get; init; } = string.Empty;
        public DateOnly StartDate { get; init; }
        public DateOnly EndDate { get; init; }
        public string Status { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }

        public Season ToSeason() => new()
        {
            Id = Id,
            LeagueId = LeagueId,
            Name = Name,
            StartDate = StartDate,
            EndDate = EndDate,
            Status = Enum.Parse<SeasonStatus>(Status),
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
        };
    }

    private sealed class WeekRow
    {
        public Guid Id { get; init; }
        public Guid SeasonId { get; init; }
        public DateOnly StartDate { get; init; }
        public DateOnly EndDate { get; init; }
        public string WeekType { get; init; } = string.Empty;

        public SeasonWeek ToWeek() => new()
        {
            Id = Id,
            SeasonId = SeasonId,
            StartDate = StartDate,
            EndDate = EndDate,
            WeekType = Enum.Parse<WeekType>(WeekType),
        };
    }
}
