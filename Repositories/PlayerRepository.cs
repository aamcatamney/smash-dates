using Dapper;
using smash_dates.Data;
using smash_dates.Models;

namespace smash_dates.Repositories;

public sealed class PlayerRepository : IPlayerRepository
{
    private readonly IDbConnectionFactory _factory;

    public PlayerRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<Guid> CreateAsync(string fullName, Gender gender, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<Guid>(
            new CommandDefinition(
                @"INSERT INTO players (full_name, gender) VALUES (@fullName, @gender) RETURNING id",
                new { fullName, gender = gender.ToString() },
                cancellationToken: ct));
    }

    public async Task<Player?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<Player>(
            new CommandDefinition(
                "SELECT id, full_name, gender, created_at, updated_at FROM players WHERE id = @id",
                new { id },
                cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Player>> SearchAsync(string query, int limit, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<Player>(
            new CommandDefinition(
                @"SELECT id, full_name, gender, created_at, updated_at FROM players
                  WHERE full_name ILIKE '%' || @query || '%'
                  ORDER BY full_name
                  LIMIT @limit",
                new { query, limit },
                cancellationToken: ct));
        return rows.AsList();
    }

    public async Task LinkAsync(Guid playerId, Guid clubId, PlayerClubType type, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync(
            new CommandDefinition(
                @"INSERT INTO player_clubs (player_id, club_id, type)
                  VALUES (@playerId, @clubId, @type)
                  ON CONFLICT (player_id, club_id) DO UPDATE SET type = EXCLUDED.type, updated_at = now()",
                new { playerId, clubId, type = type.ToString() },
                cancellationToken: ct));
    }

    public async Task<PlayerClub?> GetLinkAsync(Guid playerId, Guid clubId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<PlayerClub>(
            new CommandDefinition(
                @"SELECT id, player_id, club_id, type, created_at, updated_at
                  FROM player_clubs WHERE player_id = @playerId AND club_id = @clubId",
                new { playerId, clubId },
                cancellationToken: ct));
    }

    public async Task<IReadOnlyList<PlayerClubView>> ListByClubAsync(Guid clubId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<PlayerClubView>(
            new CommandDefinition(
                @"SELECT p.id AS player_id, p.full_name, p.gender, pc.type
                  FROM player_clubs pc
                  JOIN players p ON p.id = pc.player_id
                  WHERE pc.club_id = @clubId
                  ORDER BY p.full_name",
                new { clubId },
                cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<bool> UnlinkAsync(Guid playerId, Guid clubId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM player_clubs WHERE player_id = @playerId AND club_id = @clubId",
                new { playerId, clubId },
                cancellationToken: ct));
        return rows > 0;
    }
}
