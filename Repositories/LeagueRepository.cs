using Dapper;
using Npgsql;
using smash_dates.Data;
using smash_dates.Models;

namespace smash_dates.Repositories;

public sealed class LeagueRepository : ILeagueRepository
{
    private const string SelectColumns =
        "id, name, description, created_by, created_at, updated_at";

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
}
