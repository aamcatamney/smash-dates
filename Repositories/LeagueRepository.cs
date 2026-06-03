using Dapper;
using Npgsql;
using smash_dates.Data;
using smash_dates.Models;

namespace smash_dates.Repositories;

public sealed class LeagueRepository : ILeagueRepository
{
    private const string SelectColumns =
        "id, name, description, created_by, created_at, updated_at, " +
        "spread_weight, leg_weight, min_gap_days, target_gap_days, courts_per_match";

    private readonly IDbConnectionFactory _factory;

    public LeagueRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<League?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<League>(
            new CommandDefinition(
                $"SELECT {SelectColumns} FROM leagues WHERE id = @id",
                new { id },
                cancellationToken: ct));
    }

    public async Task<IReadOnlyList<LeagueListItem>> ListSummariesAsync(CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<LeagueListItem>(
            new CommandDefinition(
                @"SELECT l.id, l.name, l.description,
                         (SELECT count(*) FROM divisions d WHERE d.league_id = l.id)::int AS division_count,
                         (SELECT count(DISTINCT r.player_id) FROM discipline_registrations r
                          WHERE r.league_id = l.id AND r.status = 'Confirmed')::int AS player_count,
                         (SELECT s.name FROM seasons s
                          WHERE s.league_id = l.id AND s.status = 'Active'
                          ORDER BY s.start_date DESC LIMIT 1) AS active_season_name
                  FROM leagues l
                  ORDER BY l.name",
                cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<League>> ListAsync(CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<League>(
            new CommandDefinition(
                $"SELECT {SelectColumns} FROM leagues ORDER BY name",
                cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<Guid> CreateAsync(string name, string? description, Guid createdBy, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<Guid>(
            new CommandDefinition(
                @"INSERT INTO leagues (name, description, created_by)
                  VALUES (@name, @description, @createdBy)
                  RETURNING id",
                new { name, description, createdBy },
                cancellationToken: ct));
    }

    public async Task<Guid> CreateWithFirstAdminAsync(
        string name,
        string? description,
        Guid createdBy,
        Guid firstAdminUserId,
        CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        if (conn is System.Data.Common.DbConnection dbConn)
        {
            await dbConn.OpenAsync(ct);
        }
        else
        {
            conn.Open();
        }

        using var tx = conn.BeginTransaction();

        var id = await conn.ExecuteScalarAsync<Guid>(
            new CommandDefinition(
                @"INSERT INTO leagues (name, description, created_by)
                  VALUES (@name, @description, @createdBy)
                  RETURNING id",
                new { name, description, createdBy },
                transaction: tx,
                cancellationToken: ct));

        await conn.ExecuteAsync(
            new CommandDefinition(
                @"INSERT INTO league_admins (league_id, user_id, granted_by)
                  VALUES (@id, @firstAdminUserId, @createdBy)",
                new { id, firstAdminUserId, createdBy },
                transaction: tx,
                cancellationToken: ct));

        tx.Commit();
        return id;
    }

    public async Task<bool> UpdateSchedulingConfigAsync(
        Guid id, int spreadWeight, int legWeight, int minGapDays, int? targetGapDays, int courtsPerMatch, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE leagues
                  SET spread_weight = @spreadWeight, leg_weight = @legWeight,
                      min_gap_days = @minGapDays, target_gap_days = @targetGapDays,
                      courts_per_match = @courtsPerMatch, updated_at = now()
                  WHERE id = @id",
                new { id, spreadWeight, legWeight, minGapDays, targetGapDays, courtsPerMatch },
                cancellationToken: ct));
        return rows > 0;
    }
}
