using Dapper;
using smash_dates.Data;
using smash_dates.Models;

namespace smash_dates.Repositories;

public sealed class VenueRepository : IVenueRepository
{
    private const string SelectColumns =
        "id, club_id, name, courts, max_concurrent_matches, address, created_at, updated_at";

    private readonly IDbConnectionFactory _factory;

    public VenueRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<Venue?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<Venue>(
            new CommandDefinition(
                $"SELECT {SelectColumns} FROM venues WHERE id = @id",
                new { id },
                cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Venue>> ListByClubAsync(Guid clubId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<Venue>(
            new CommandDefinition(
                $@"SELECT {SelectColumns} FROM venues
                   WHERE club_id = @clubId
                   ORDER BY name",
                new { clubId },
                cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<Guid> CreateAsync(Guid clubId, string name, int courts, int maxConcurrentMatches, string? address, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<Guid>(
            new CommandDefinition(
                @"INSERT INTO venues (club_id, name, courts, max_concurrent_matches, address)
                  VALUES (@clubId, @name, @courts, @maxConcurrentMatches, @address)
                  RETURNING id",
                new { clubId, name, courts, maxConcurrentMatches, address },
                cancellationToken: ct));
    }

    public async Task<bool> UpdateAsync(Guid id, string name, int courts, int maxConcurrentMatches, string? address, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE venues
                  SET name = @name, courts = @courts, max_concurrent_matches = @maxConcurrentMatches, address = @address, updated_at = now()
                  WHERE id = @id",
                new { id, name, courts, maxConcurrentMatches, address },
                cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM venues WHERE id = @id",
                new { id },
                cancellationToken: ct));
        return rows > 0;
    }
}
