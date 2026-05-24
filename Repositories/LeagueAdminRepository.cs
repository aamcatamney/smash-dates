using Dapper;
using smash_dates.Data;
using smash_dates.Models;

namespace smash_dates.Repositories;

public sealed class LeagueAdminRepository : ILeagueAdminRepository
{
    private const string SelectColumns = "league_id, user_id, granted_at, granted_by";

    private readonly IDbConnectionFactory _factory;

    public LeagueAdminRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<bool> IsAdminAsync(Guid leagueId, Guid userId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                @"SELECT EXISTS(SELECT 1 FROM league_admins
                                WHERE league_id = @leagueId AND user_id = @userId)",
                new { leagueId, userId },
                cancellationToken: ct));
    }

    public async Task<IReadOnlyList<LeagueAdminGrant>> ListByLeagueAsync(Guid leagueId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<LeagueAdminGrant>(
            new CommandDefinition(
                $"SELECT {SelectColumns} FROM league_admins WHERE league_id = @leagueId ORDER BY granted_at",
                new { leagueId },
                cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<int> CountByLeagueAsync(Guid leagueId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM league_admins WHERE league_id = @leagueId",
                new { leagueId },
                cancellationToken: ct));
    }

    public async Task GrantAsync(Guid leagueId, Guid userId, Guid? grantedBy, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync(
            new CommandDefinition(
                @"INSERT INTO league_admins (league_id, user_id, granted_by)
                  VALUES (@leagueId, @userId, @grantedBy)
                  ON CONFLICT (league_id, user_id) DO NOTHING",
                new { leagueId, userId, grantedBy },
                cancellationToken: ct));
    }

    public async Task<bool> RevokeAsync(Guid leagueId, Guid userId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM league_admins WHERE league_id = @leagueId AND user_id = @userId",
                new { leagueId, userId },
                cancellationToken: ct));
        return rows > 0;
    }
}
