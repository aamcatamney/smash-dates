using Dapper;
using smash_dates.Models;
using smash_dates.Services.Auth;
using Npgsql;

namespace smash_dates.IntegrationTests.Infrastructure;

public sealed class TestDataSeeder
{
    private readonly string _connectionString;
    private readonly IPasswordHasher _hasher = new BCryptPasswordHasher();

    public TestDataSeeder(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<User> CreateUserAsync(
        string email,
        string password,
        string? displayName = null,
        bool isActive = true)
    {
        var hash = _hasher.Hash(password);
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var id = await conn.ExecuteScalarAsync<Guid>(
            @"INSERT INTO users (email, password_hash, display_name, is_active)
              VALUES (lower(@email), @hash, @displayName, @isActive)
              RETURNING id",
            new { email, hash, displayName, isActive });

        return new User
        {
            Id = id,
            Email = email.ToLowerInvariant(),
            PasswordHash = hash,
            DisplayName = displayName,
            IsActive = isActive,
        };
    }

    public async Task<User> CreateSystemAdminUserAsync(string email, string password, string? displayName = null)
    {
        var hash = _hasher.Hash(password);
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var id = await conn.ExecuteScalarAsync<Guid>(
            @"INSERT INTO users (email, password_hash, display_name, is_active, is_system_admin)
              VALUES (lower(@email), @hash, @displayName, true, true)
              RETURNING id",
            new { email, hash, displayName });
        return new User
        {
            Id = id,
            Email = email.ToLowerInvariant(),
            PasswordHash = hash,
            DisplayName = displayName,
            IsActive = true,
            IsSystemAdmin = true,
        };
    }

    public async Task<Guid> CreateLeagueAsync(string name, Guid createdBy, string? description = null)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        return await conn.ExecuteScalarAsync<Guid>(
            @"INSERT INTO leagues (name, description, created_by)
              VALUES (@name, @description, @createdBy)
              RETURNING id",
            new { name, description, createdBy });
    }

    public async Task GrantLeagueAdminAsync(Guid leagueId, Guid userId, Guid? grantedBy = null)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync(
            @"INSERT INTO league_admins (league_id, user_id, granted_by)
              VALUES (@leagueId, @userId, @grantedBy)
              ON CONFLICT DO NOTHING",
            new { leagueId, userId, grantedBy });
    }

    public async Task<Guid> CreateClubAsync(
        string name,
        string shortCode,
        string contactEmail = "club@example.com",
        string? notes = null)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        return await conn.ExecuteScalarAsync<Guid>(
            @"INSERT INTO clubs (name, short_code, contact_email, notes)
              VALUES (@name, @shortCode, @contactEmail, @notes)
              RETURNING id",
            new { name, shortCode, contactEmail, notes });
    }

    public async Task GrantClubAdminAsync(Guid clubId, Guid userId, Guid? grantedBy = null)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync(
            @"INSERT INTO club_admins (club_id, user_id, granted_by)
              VALUES (@clubId, @userId, @grantedBy)
              ON CONFLICT DO NOTHING",
            new { clubId, userId, grantedBy });
    }

    public async Task<Guid> CreateMembershipAsync(
        Guid clubId,
        Guid leagueId,
        MembershipStatus status = MembershipStatus.Pending,
        Guid? invitedBy = null)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        return await conn.ExecuteScalarAsync<Guid>(
            @"INSERT INTO club_league_memberships (club_id, league_id, status, invited_by)
              VALUES (@clubId, @leagueId, @status, @invitedBy)
              RETURNING id",
            new { clubId, leagueId, status = status.ToString(), invitedBy });
    }

    public async Task<Guid> CreateDivisionAsync(
        Guid leagueId,
        string name,
        DivisionGender gender,
        int rank,
        int rubbersPerMatch,
        int winPoints = 2,
        int drawPoints = 1,
        int lossPoints = 0)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        return await conn.ExecuteScalarAsync<Guid>(
            @"INSERT INTO divisions (league_id, name, gender, rank, rubbers_per_match, win_points, draw_points, loss_points)
              VALUES (@leagueId, @name, @gender, @rank, @rubbersPerMatch, @winPoints, @drawPoints, @lossPoints)
              RETURNING id",
            new { leagueId, name, gender = gender.ToString(), rank, rubbersPerMatch, winPoints, drawPoints, lossPoints });
    }
}
