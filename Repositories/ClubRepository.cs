using Dapper;
using Npgsql;
using smash_dates.Data;
using smash_dates.Models;

namespace smash_dates.Repositories;

public sealed class ClubRepository : IClubRepository
{
    private const string SelectColumns =
        "id, name, short_code, contact_email, notes, created_at, updated_at";

    private readonly IDbConnectionFactory _factory;

    public ClubRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<Club?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<Club>(
            new CommandDefinition(
                $"SELECT {SelectColumns} FROM clubs WHERE id = @id",
                new { id },
                cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Club>> ListAsync(CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<Club>(
            new CommandDefinition(
                $"SELECT {SelectColumns} FROM clubs ORDER BY name",
                cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<Guid> CreateWithFirstAdminAsync(
        string name,
        string shortCode,
        string contactEmail,
        string? notes,
        Guid firstAdminUserId,
        Guid grantedBy,
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
                @"INSERT INTO clubs (name, short_code, contact_email, notes)
                  VALUES (@name, @shortCode, @contactEmail, @notes)
                  RETURNING id",
                new { name, shortCode, contactEmail, notes },
                transaction: tx,
                cancellationToken: ct));

        await conn.ExecuteAsync(
            new CommandDefinition(
                @"INSERT INTO club_admins (club_id, user_id, granted_by)
                  VALUES (@id, @firstAdminUserId, @grantedBy)",
                new { id, firstAdminUserId, grantedBy },
                transaction: tx,
                cancellationToken: ct));

        tx.Commit();
        return id;
    }

    public async Task<bool> UpdateAsync(
        Guid id,
        string name,
        string shortCode,
        string contactEmail,
        string? notes,
        CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE clubs
                  SET name = @name,
                      short_code = @shortCode,
                      contact_email = @contactEmail,
                      notes = @notes,
                      updated_at = now()
                  WHERE id = @id",
                new { id, name, shortCode, contactEmail, notes },
                cancellationToken: ct));
        return rows > 0;
    }
}
