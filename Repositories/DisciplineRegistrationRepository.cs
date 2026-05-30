using Dapper;
using smash_dates.Data;
using smash_dates.Models;

namespace smash_dates.Repositories;

public sealed class DisciplineRegistrationRepository : IDisciplineRegistrationRepository
{
    private const string ViewSelect =
        @"SELECT r.id, r.player_id, p.full_name AS player_name, p.gender,
                 r.club_id, c.short_code AS club_short_code,
                 r.league_id, l.name AS league_name, r.discipline, r.status
          FROM discipline_registrations r
          JOIN players p ON p.id = r.player_id
          JOIN clubs c ON c.id = r.club_id
          JOIN leagues l ON l.id = r.league_id";

    private readonly IDbConnectionFactory _factory;

    public DisciplineRegistrationRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<Guid> CreateAsync(Guid playerId, Guid clubId, Guid leagueId, Discipline discipline, Guid requestedBy, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<Guid>(
            new CommandDefinition(
                @"INSERT INTO discipline_registrations (player_id, club_id, league_id, discipline, requested_by)
                  VALUES (@playerId, @clubId, @leagueId, @discipline, @requestedBy)
                  RETURNING id",
                new { playerId, clubId, leagueId, discipline = discipline.ToString(), requestedBy },
                cancellationToken: ct));
    }

    public async Task<DisciplineRegistration?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<DisciplineRegistration>(
            new CommandDefinition(
                @"SELECT id, player_id, club_id, league_id, discipline, status,
                         requested_by, responded_by, requested_at, responded_at
                  FROM discipline_registrations WHERE id = @id",
                new { id },
                cancellationToken: ct));
    }

    public async Task<DisciplineRegistration?> GetConfirmedAsync(Guid playerId, Guid leagueId, Discipline discipline, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<DisciplineRegistration>(
            new CommandDefinition(
                @"SELECT id, player_id, club_id, league_id, discipline, status,
                         requested_by, responded_by, requested_at, responded_at
                  FROM discipline_registrations
                  WHERE player_id = @playerId AND league_id = @leagueId
                    AND discipline = @discipline AND status = 'Confirmed'",
                new { playerId, leagueId, discipline = discipline.ToString() },
                cancellationToken: ct));
    }

    public async Task<bool> ConfirmAsync(Guid id, Guid respondedBy, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE discipline_registrations
                  SET status = 'Confirmed', responded_by = @respondedBy, responded_at = now()
                  WHERE id = @id AND status = 'Pending'",
                new { id, respondedBy },
                cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> RejectAsync(Guid id, Guid respondedBy, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE discipline_registrations
                  SET status = 'Rejected', responded_by = @respondedBy, responded_at = now()
                  WHERE id = @id AND status = 'Pending'",
                new { id, respondedBy },
                cancellationToken: ct));
        return rows > 0;
    }

    public async Task<IReadOnlyList<RegistrationView>> ListByLeagueAsync(Guid leagueId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<RegistrationView>(
            new CommandDefinition(
                $"{ViewSelect} WHERE r.league_id = @leagueId ORDER BY r.status, p.full_name",
                new { leagueId },
                cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<RegistrationView>> ListByClubAsync(Guid clubId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<RegistrationView>(
            new CommandDefinition(
                $"{ViewSelect} WHERE r.club_id = @clubId ORDER BY r.status, p.full_name",
                new { clubId },
                cancellationToken: ct));
        return rows.AsList();
    }
}
