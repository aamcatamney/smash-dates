using Dapper;
using smash_dates.Data;
using smash_dates.Models;

namespace smash_dates.Repositories;

public sealed class BlockedDateRepository : IBlockedDateRepository
{
    private const string SelectColumns =
        "id, club_id, scope, venue_id, team_id, start_date, end_date, reason, created_at";

    private readonly IDbConnectionFactory _factory;

    public BlockedDateRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<BlockedDate?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var row = await conn.QuerySingleOrDefaultAsync<Row>(
            new CommandDefinition(
                $"SELECT {SelectColumns} FROM blocked_dates WHERE id = @id",
                new { id },
                cancellationToken: ct));
        return row?.ToModel();
    }

    public async Task<IReadOnlyList<BlockedDate>> ListByClubAsync(Guid clubId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<Row>(
            new CommandDefinition(
                $@"SELECT {SelectColumns} FROM blocked_dates
                   WHERE club_id = @clubId
                   ORDER BY start_date, scope",
                new { clubId },
                cancellationToken: ct));
        return rows.Select(r => r.ToModel()).ToList();
    }

    public async Task<Guid> CreateAsync(
        Guid clubId,
        BlockedDateScope scope,
        Guid? venueId,
        Guid? teamId,
        DateOnly startDate,
        DateOnly endDate,
        string reason,
        CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<Guid>(
            new CommandDefinition(
                @"INSERT INTO blocked_dates (club_id, scope, venue_id, team_id, start_date, end_date, reason)
                  VALUES (@clubId, @scope, @venueId, @teamId, @startDate, @endDate, @reason)
                  RETURNING id",
                new { clubId, scope = scope.ToString(), venueId, teamId, startDate, endDate, reason },
                cancellationToken: ct));
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM blocked_dates WHERE id = @id",
                new { id },
                cancellationToken: ct));
        return rows > 0;
    }

    private sealed class Row
    {
        public Guid Id { get; init; }
        public Guid ClubId { get; init; }
        public string Scope { get; init; } = string.Empty;
        public Guid? VenueId { get; init; }
        public Guid? TeamId { get; init; }
        public DateOnly StartDate { get; init; }
        public DateOnly EndDate { get; init; }
        public string Reason { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }

        public BlockedDate ToModel() => new()
        {
            Id = Id,
            ClubId = ClubId,
            Scope = Enum.Parse<BlockedDateScope>(Scope),
            VenueId = VenueId,
            TeamId = TeamId,
            StartDate = StartDate,
            EndDate = EndDate,
            Reason = Reason,
            CreatedAt = CreatedAt,
        };
    }
}
