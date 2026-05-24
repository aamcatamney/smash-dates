using Dapper;
using smash_dates.Data;
using smash_dates.Models;

namespace smash_dates.Repositories;

public sealed class UserRepository : IUserRepository
{
    private const string SelectColumns =
        "id, email, password_hash, display_name, is_active, is_system_admin, created_at, updated_at";

    private readonly IDbConnectionFactory _factory;

    public UserRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<User>(
            new CommandDefinition(
                $"SELECT {SelectColumns} FROM users WHERE id = @id",
                new { id },
                cancellationToken: ct));
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<User>(
            new CommandDefinition(
                $"SELECT {SelectColumns} FROM users WHERE lower(email) = lower(@email)",
                new { email },
                cancellationToken: ct));
    }

    public async Task<Guid> CreateAsync(string email, string passwordHash, string? displayName, CancellationToken ct = default)
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

        var hasAnyUser = await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT EXISTS(SELECT 1 FROM users)",
                transaction: tx,
                cancellationToken: ct));

        var id = await conn.ExecuteScalarAsync<Guid>(
            new CommandDefinition(
                @"INSERT INTO users (email, password_hash, display_name, is_system_admin)
                  VALUES (lower(@email), @passwordHash, @displayName, @isSystemAdmin)
                  RETURNING id",
                new { email, passwordHash, displayName, isSystemAdmin = !hasAnyUser },
                transaction: tx,
                cancellationToken: ct));

        tx.Commit();
        return id;
    }

    public async Task<bool> UpdatePasswordAsync(Guid id, string passwordHash, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE users SET password_hash = @passwordHash, updated_at = now()
                  WHERE id = @id",
                new { id, passwordHash },
                cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE users SET is_active = @isActive, updated_at = now()
                  WHERE id = @id",
                new { id, isActive },
                cancellationToken: ct));
        return rows > 0;
    }
}
