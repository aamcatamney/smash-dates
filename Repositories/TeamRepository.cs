using Dapper;
using smash_dates.Data;
using smash_dates.Models;

namespace smash_dates.Repositories;

public sealed class TeamRepository : ITeamRepository
{
    private const string SelectColumns =
        "id, club_id, name, gender, created_at, updated_at";

    private readonly IDbConnectionFactory _factory;

    public TeamRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<Team?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var row = await conn.QuerySingleOrDefaultAsync<TeamRow>(
            new CommandDefinition(
                $"SELECT {SelectColumns} FROM teams WHERE id = @id",
                new { id },
                cancellationToken: ct));
        return row?.ToTeam();
    }

    public async Task<IReadOnlyList<Team>> ListByClubAsync(Guid clubId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<TeamRow>(
            new CommandDefinition(
                $@"SELECT {SelectColumns} FROM teams
                   WHERE club_id = @clubId
                   ORDER BY gender, name",
                new { clubId },
                cancellationToken: ct));
        return rows.Select(r => r.ToTeam()).ToList();
    }

    public async Task<IReadOnlyList<Team>> ListByLeagueAcceptedMembersAsync(Guid leagueId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<TeamRow>(
            new CommandDefinition(
                $@"SELECT {SelectColumns} FROM teams t
                   WHERE EXISTS (
                       SELECT 1 FROM club_league_memberships m
                       WHERE m.club_id = t.club_id AND m.league_id = @leagueId AND m.status = 'Accepted')
                   ORDER BY t.gender, t.name",
                new { leagueId },
                cancellationToken: ct));
        return rows.Select(r => r.ToTeam()).ToList();
    }

    public async Task<Guid> CreateAsync(Guid clubId, string name, DivisionGender gender, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<Guid>(
            new CommandDefinition(
                @"INSERT INTO teams (club_id, name, gender)
                  VALUES (@clubId, @name, @gender)
                  RETURNING id",
                new { clubId, name, gender = gender.ToString() },
                cancellationToken: ct));
    }

    public async Task<bool> UpdateNameAsync(Guid id, string name, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE teams SET name = @name, updated_at = now() WHERE id = @id",
                new { id, name },
                cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM teams WHERE id = @id",
                new { id },
                cancellationToken: ct));
        return rows > 0;
    }

    private sealed class TeamRow
    {
        public Guid Id { get; init; }
        public Guid ClubId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Gender { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }

        public Team ToTeam() => new()
        {
            Id = Id,
            ClubId = ClubId,
            Name = Name,
            Gender = Enum.Parse<DivisionGender>(Gender),
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
        };
    }
}
