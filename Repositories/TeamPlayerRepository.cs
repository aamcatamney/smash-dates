using Dapper;
using smash_dates.Data;
using smash_dates.Models;

namespace smash_dates.Repositories;

public sealed class TeamPlayerRepository : ITeamPlayerRepository
{
    private readonly IDbConnectionFactory _factory;

    public TeamPlayerRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<SquadMember>> ListByTeamAsync(Guid teamId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<SquadMember>(
            new CommandDefinition(
                @"SELECT p.id AS player_id, p.full_name, p.gender
                  FROM team_players tp
                  JOIN players p ON p.id = tp.player_id
                  WHERE tp.team_id = @teamId
                  ORDER BY p.full_name",
                new { teamId },
                cancellationToken: ct));
        return rows.AsList();
    }

    public async Task AddAsync(Guid teamId, Guid playerId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync(
            new CommandDefinition(
                @"INSERT INTO team_players (team_id, player_id) VALUES (@teamId, @playerId)
                  ON CONFLICT (team_id, player_id) DO NOTHING",
                new { teamId, playerId },
                cancellationToken: ct));
    }

    public async Task<bool> RemoveAsync(Guid teamId, Guid playerId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM team_players WHERE team_id = @teamId AND player_id = @playerId",
                new { teamId, playerId },
                cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> IsEligibleAsync(Guid playerId, Guid clubId, Discipline discipline, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                @"SELECT EXISTS(
                    SELECT 1 FROM discipline_registrations
                    WHERE player_id = @playerId AND club_id = @clubId
                      AND discipline = @discipline AND status = 'Confirmed')",
                new { playerId, clubId, discipline = discipline.ToString() },
                cancellationToken: ct));
    }
}
