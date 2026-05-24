using Dapper;
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
}
