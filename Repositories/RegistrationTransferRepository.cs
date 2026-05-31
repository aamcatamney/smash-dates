using Dapper;
using smash_dates.Data;
using smash_dates.Models;

namespace smash_dates.Repositories;

public sealed class RegistrationTransferRepository : IRegistrationTransferRepository
{
    private const string ViewSelect =
        @"SELECT t.id, t.player_id, p.full_name AS player_name, t.discipline,
                 t.from_club_id, fc.short_code AS from_short_code,
                 t.to_club_id, tc.short_code AS to_short_code,
                 t.league_id, l.name AS league_name, t.status,
                 t.releasing_approved, t.league_approved
          FROM registration_transfers t
          JOIN players p ON p.id = t.player_id
          JOIN clubs fc ON fc.id = t.from_club_id
          JOIN clubs tc ON tc.id = t.to_club_id
          JOIN leagues l ON l.id = t.league_id";

    private readonly IDbConnectionFactory _factory;

    public RegistrationTransferRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<Guid> CreateAsync(Guid playerId, Guid leagueId, Discipline discipline, Guid fromClubId, Guid toClubId, Guid initiatedBy, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<Guid>(
            new CommandDefinition(
                @"INSERT INTO registration_transfers (player_id, league_id, discipline, from_club_id, to_club_id, initiated_by)
                  VALUES (@playerId, @leagueId, @discipline, @fromClubId, @toClubId, @initiatedBy)
                  RETURNING id",
                new { playerId, leagueId, discipline = discipline.ToString(), fromClubId, toClubId, initiatedBy },
                cancellationToken: ct));
    }

    public async Task<RegistrationTransfer?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<RegistrationTransfer>(
            new CommandDefinition(
                @"SELECT id, player_id, league_id, discipline, from_club_id, to_club_id, status,
                         releasing_approved, league_approved, initiated_by, created_at, resolved_at
                  FROM registration_transfers WHERE id = @id",
                new { id },
                cancellationToken: ct));
    }

    public Task<bool?> SetReleasingApprovedAsync(Guid id, CancellationToken ct = default) =>
        SetApprovedAsync(id, "releasing_approved", ct);

    public Task<bool?> SetLeagueApprovedAsync(Guid id, CancellationToken ct = default) =>
        SetApprovedAsync(id, "league_approved", ct);

    private async Task<bool?> SetApprovedAsync(Guid id, string column, CancellationToken ct)
    {
        using var conn = _factory.Create();
        // RETURNING null (no row) means the transfer was not Pending.
        return await conn.ExecuteScalarAsync<bool?>(
            new CommandDefinition(
                $@"UPDATE registration_transfers SET {column} = true
                   WHERE id = @id AND status = 'Pending'
                   RETURNING (releasing_approved AND league_approved)",
                new { id },
                cancellationToken: ct));
    }

    public async Task<bool> RejectAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE registration_transfers SET status = 'Rejected', resolved_at = now()
                  WHERE id = @id AND status = 'Pending'",
                new { id },
                cancellationToken: ct));
        return rows > 0;
    }

    public async Task CompleteAsync(RegistrationTransfer t, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        if (conn is System.Data.Common.DbConnection dbConn) await dbConn.OpenAsync(ct);
        else conn.Open();

        using var tx = conn.BeginTransaction();

        await conn.ExecuteAsync(new CommandDefinition(
            @"UPDATE registration_transfers SET status = 'Completed', resolved_at = now()
              WHERE id = @id AND status = 'Pending'",
            new { id = t.Id }, transaction: tx, cancellationToken: ct));

        await conn.ExecuteAsync(new CommandDefinition(
            @"UPDATE discipline_registrations SET club_id = @toClub
              WHERE player_id = @player AND league_id = @league
                AND discipline = @discipline AND status = 'Confirmed'",
            new { toClub = t.ToClubId, player = t.PlayerId, league = t.LeagueId, discipline = t.Discipline.ToString() },
            transaction: tx, cancellationToken: ct));

        await conn.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO player_clubs (player_id, club_id, type) VALUES (@player, @toClub, 'Member')
              ON CONFLICT (player_id, club_id) DO UPDATE SET type = 'Member', updated_at = now()",
            new { player = t.PlayerId, toClub = t.ToClubId },
            transaction: tx, cancellationToken: ct));

        tx.Commit();
    }

    public async Task<IReadOnlyList<TransferView>> ListByClubAsync(Guid clubId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<TransferView>(
            new CommandDefinition(
                $"{ViewSelect} WHERE t.from_club_id = @clubId OR t.to_club_id = @clubId ORDER BY t.status, t.created_at DESC",
                new { clubId },
                cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<TransferView>> ListByLeagueAsync(Guid leagueId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<TransferView>(
            new CommandDefinition(
                $"{ViewSelect} WHERE t.league_id = @leagueId ORDER BY t.status, t.created_at DESC",
                new { leagueId },
                cancellationToken: ct));
        return rows.AsList();
    }
}
