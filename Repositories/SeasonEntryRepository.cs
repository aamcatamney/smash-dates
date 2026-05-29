using Dapper;
using smash_dates.Data;
using smash_dates.Models;

namespace smash_dates.Repositories;

public sealed class SeasonEntryRepository : ISeasonEntryRepository
{
    private readonly IDbConnectionFactory _factory;

    public SeasonEntryRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<SeasonEntry?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<SeasonEntry>(
            new CommandDefinition(
                "SELECT id, season_id, division_id, team_id, created_at FROM season_entries WHERE id = @id",
                new { id },
                cancellationToken: ct));
    }

    public async Task<IReadOnlyList<SeasonEntryView>> ListBySeasonAsync(Guid seasonId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<ViewRow>(
            new CommandDefinition(
                @"SELECT e.id, e.season_id, e.division_id, d.name AS division_name,
                         e.team_id, t.name AS team_name, d.gender
                  FROM season_entries e
                  JOIN divisions d ON d.id = e.division_id
                  JOIN teams t ON t.id = e.team_id
                  WHERE e.season_id = @seasonId
                  ORDER BY d.gender, d.rank, t.name",
                new { seasonId },
                cancellationToken: ct));
        return rows.Select(r => r.ToView()).ToList();
    }

    public async Task<Guid> CreateAsync(Guid seasonId, Guid divisionId, Guid teamId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<Guid>(
            new CommandDefinition(
                @"INSERT INTO season_entries (season_id, division_id, team_id)
                  VALUES (@seasonId, @divisionId, @teamId)
                  RETURNING id",
                new { seasonId, divisionId, teamId },
                cancellationToken: ct));
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM season_entries WHERE id = @id",
                new { id },
                cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> ExistsForTeamAsync(Guid teamId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT EXISTS(SELECT 1 FROM season_entries WHERE team_id = @teamId)",
                new { teamId },
                cancellationToken: ct));
    }

    private sealed class ViewRow
    {
        public Guid Id { get; init; }
        public Guid SeasonId { get; init; }
        public Guid DivisionId { get; init; }
        public string DivisionName { get; init; } = string.Empty;
        public Guid TeamId { get; init; }
        public string TeamName { get; init; } = string.Empty;
        public string Gender { get; init; } = string.Empty;

        public SeasonEntryView ToView() => new()
        {
            Id = Id,
            SeasonId = SeasonId,
            DivisionId = DivisionId,
            DivisionName = DivisionName,
            TeamId = TeamId,
            TeamName = TeamName,
            Gender = Enum.Parse<DivisionGender>(Gender),
        };
    }
}
