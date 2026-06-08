using Dapper;
using smash_dates.Data;
using smash_dates.Models;

namespace smash_dates.Repositories;

public sealed class SessionHostRepository : ISessionHostRepository
{
    private readonly IDbConnectionFactory _factory;

    public SessionHostRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<bool> IsHostAsync(Guid clubId, Guid userId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT EXISTS (SELECT 1 FROM session_hosts WHERE club_id = @clubId AND user_id = @userId)",
                new { clubId, userId },
                cancellationToken: ct));
    }

    public async Task<IReadOnlyList<SessionHostGrant>> ListByClubAsync(Guid clubId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<SessionHostGrant>(
            new CommandDefinition(
                @"SELECT club_id, user_id, granted_at, granted_by
                  FROM session_hosts WHERE club_id = @clubId ORDER BY granted_at",
                new { clubId },
                cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<SessionHostGrantView>> ListByUserAsync(Guid userId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<SessionHostGrantView>(
            new CommandDefinition(
                @"SELECT sh.club_id, c.name AS club_name
                  FROM session_hosts sh
                  JOIN clubs c ON c.id = sh.club_id
                  WHERE sh.user_id = @userId
                  ORDER BY c.name",
                new { userId },
                cancellationToken: ct));
        return rows.AsList();
    }

    public async Task GrantAsync(Guid clubId, Guid userId, Guid? grantedBy, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync(
            new CommandDefinition(
                @"INSERT INTO session_hosts (club_id, user_id, granted_by)
                  VALUES (@clubId, @userId, @grantedBy)
                  ON CONFLICT (club_id, user_id) DO NOTHING",
                new { clubId, userId, grantedBy },
                cancellationToken: ct));
    }

    public async Task<bool> RevokeAsync(Guid clubId, Guid userId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM session_hosts WHERE club_id = @clubId AND user_id = @userId",
                new { clubId, userId },
                cancellationToken: ct));
        return rows > 0;
    }
}
