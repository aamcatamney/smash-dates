using Dapper;
using smash_dates.Data;
using smash_dates.Models;

namespace smash_dates.Repositories;

public sealed class ClubAdminRepository : IClubAdminRepository
{
    private const string SelectColumns = "club_id, user_id, granted_at, granted_by";

    private readonly IDbConnectionFactory _factory;

    public ClubAdminRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<bool> IsAdminAsync(Guid clubId, Guid userId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                @"SELECT EXISTS(SELECT 1 FROM club_admins
                                WHERE club_id = @clubId AND user_id = @userId)",
                new { clubId, userId },
                cancellationToken: ct));
    }

    public async Task<IReadOnlyList<ClubAdminGrant>> ListByClubAsync(Guid clubId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<ClubAdminGrant>(
            new CommandDefinition(
                $"SELECT {SelectColumns} FROM club_admins WHERE club_id = @clubId ORDER BY granted_at",
                new { clubId },
                cancellationToken: ct));
        return rows.AsList();
    }

    public async Task GrantAsync(Guid clubId, Guid userId, Guid? grantedBy, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync(
            new CommandDefinition(
                @"INSERT INTO club_admins (club_id, user_id, granted_by)
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
                "DELETE FROM club_admins WHERE club_id = @clubId AND user_id = @userId",
                new { clubId, userId },
                cancellationToken: ct));
        return rows > 0;
    }

    public async Task<RevokeResult> RevokeUnlessLastAsync(Guid clubId, Guid userId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        // Single statement: only delete when more than one admin exists for the club.
        // RETURNING distinguishes "deleted" from "no rows touched". A separate existence
        // probe tells us whether the grant existed at all - which lets us return
        // NotAdmin vs WouldBeLastAdmin. The probe and the delete are evaluated together
        // by Postgres in a single CTE, eliminating the TOCTOU window that an
        // application-level count + delete would have.
        var outcome = await conn.QuerySingleOrDefaultAsync<string?>(
            new CommandDefinition(
                @"WITH existing AS (
                      SELECT 1 FROM club_admins
                      WHERE club_id = @clubId AND user_id = @userId
                  ),
                  attempt AS (
                      DELETE FROM club_admins
                      WHERE club_id = @clubId
                        AND user_id = @userId
                        AND (SELECT COUNT(*) FROM club_admins WHERE club_id = @clubId) > 1
                      RETURNING 1
                  )
                  SELECT CASE
                      WHEN NOT EXISTS (SELECT 1 FROM existing) THEN 'NotAdmin'
                      WHEN EXISTS (SELECT 1 FROM attempt) THEN 'Revoked'
                      ELSE 'WouldBeLastAdmin'
                  END",
                new { clubId, userId },
                cancellationToken: ct));

        return outcome switch
        {
            "Revoked" => RevokeResult.Revoked,
            "WouldBeLastAdmin" => RevokeResult.WouldBeLastAdmin,
            _ => RevokeResult.NotAdmin,
        };
    }
}
