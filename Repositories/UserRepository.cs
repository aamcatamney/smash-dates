using Dapper;
using Npgsql;
using smash_dates.Data;
using smash_dates.Models;

namespace smash_dates.Repositories;

public sealed class UserRepository : IUserRepository
{
    private const string SelectColumns =
        "id, email, password_hash, display_name, is_active, is_system_admin, email_verified, created_at, updated_at";

    // Constraint names referenced by Postgres exceptions (SQLSTATE 23505).
    private const string SystemAdminUniqueConstraint = "ux_users_one_system_admin";

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

        // The bootstrap SystemAdmin (first user) is verified automatically; everyone else must
        // verify their email before they can log in.
        var insertSql = @"INSERT INTO users (email, password_hash, display_name, is_system_admin, email_verified)
                          VALUES (lower(@email), @passwordHash, @displayName, @isSystemAdmin, @emailVerified)
                          RETURNING id";

        Guid id;
        try
        {
            id = await conn.ExecuteScalarAsync<Guid>(
                new CommandDefinition(
                    insertSql,
                    new { email, passwordHash, displayName, isSystemAdmin = !hasAnyUser, emailVerified = !hasAnyUser },
                    transaction: tx,
                    cancellationToken: ct));
        }
        catch (PostgresException ex) when (ex.SqlState == "23505" && ex.ConstraintName == SystemAdminUniqueConstraint)
        {
            // Lost the race to become the first SystemAdmin: a concurrent registration
            // committed first. Retry as a non-admin user inside the same transaction.
            id = await conn.ExecuteScalarAsync<Guid>(
                new CommandDefinition(
                    insertSql,
                    new { email, passwordHash, displayName, isSystemAdmin = false, emailVerified = false },
                    transaction: tx,
                    cancellationToken: ct));
        }

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

    public async Task<bool> UpdateDisplayNameAsync(Guid id, string? displayName, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE users SET display_name = @displayName, updated_at = now()
                  WHERE id = @id",
                new { id, displayName },
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

    public async Task<bool> SetEmailVerifiedAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                "UPDATE users SET email_verified = true, updated_at = now() WHERE id = @id",
                new { id },
                cancellationToken: ct));
        return rows > 0;
    }
}
