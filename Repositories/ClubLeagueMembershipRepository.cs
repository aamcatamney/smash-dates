using Dapper;
using smash_dates.Data;
using smash_dates.Models;

namespace smash_dates.Repositories;

public sealed class ClubLeagueMembershipRepository : IClubLeagueMembershipRepository
{
    private const string SelectColumns =
        "id, club_id, league_id, status, invited_at, invited_by, responded_at, responded_by";

    private readonly IDbConnectionFactory _factory;

    public ClubLeagueMembershipRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<ClubLeagueMembership?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var row = await conn.QuerySingleOrDefaultAsync<Row>(
            new CommandDefinition(
                $"SELECT {SelectColumns} FROM club_league_memberships WHERE id = @id",
                new { id },
                cancellationToken: ct));
        return row?.ToModel();
    }

    public async Task<IReadOnlyList<ClubLeagueMembership>> ListByLeagueAsync(Guid leagueId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<Row>(
            new CommandDefinition(
                $"SELECT {SelectColumns} FROM club_league_memberships WHERE league_id = @leagueId ORDER BY invited_at",
                new { leagueId },
                cancellationToken: ct));
        return rows.Select(r => r.ToModel()).ToList();
    }

    public async Task<IReadOnlyList<ClubLeagueMembership>> ListByClubAsync(Guid clubId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<Row>(
            new CommandDefinition(
                $"SELECT {SelectColumns} FROM club_league_memberships WHERE club_id = @clubId ORDER BY invited_at",
                new { clubId },
                cancellationToken: ct));
        return rows.Select(r => r.ToModel()).ToList();
    }

    public async Task<Guid> InviteAsync(Guid clubId, Guid leagueId, Guid invitedBy, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<Guid>(
            new CommandDefinition(
                @"INSERT INTO club_league_memberships (club_id, league_id, status, invited_by)
                  VALUES (@clubId, @leagueId, 'Pending', @invitedBy)
                  RETURNING id",
                new { clubId, leagueId, invitedBy },
                cancellationToken: ct));
    }

    public async Task<bool> TransitionFromPendingAsync(
        Guid membershipId,
        MembershipStatus newStatus,
        Guid respondedBy,
        CancellationToken ct = default)
    {
        if (newStatus is not (MembershipStatus.Accepted or MembershipStatus.Declined))
        {
            throw new ArgumentOutOfRangeException(nameof(newStatus), "Only Accepted or Declined valid here.");
        }

        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE club_league_memberships
                  SET status = @newStatus,
                      responded_at = now(),
                      responded_by = @respondedBy
                  WHERE id = @membershipId AND status = 'Pending'",
                new { membershipId, newStatus = newStatus.ToString(), respondedBy },
                cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> TransitionFromAcceptedAsync(
        Guid membershipId,
        MembershipStatus newStatus,
        Guid respondedBy,
        CancellationToken ct = default)
    {
        if (newStatus is not (MembershipStatus.Withdrawn or MembershipStatus.Expelled))
        {
            throw new ArgumentOutOfRangeException(nameof(newStatus), "Only Withdrawn or Expelled valid here.");
        }

        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE club_league_memberships
                  SET status = @newStatus,
                      responded_at = now(),
                      responded_by = @respondedBy
                  WHERE id = @membershipId AND status = 'Accepted'",
                new { membershipId, newStatus = newStatus.ToString(), respondedBy },
                cancellationToken: ct));
        return rows > 0;
    }

    private sealed class Row
    {
        public Guid Id { get; init; }
        public Guid ClubId { get; init; }
        public Guid LeagueId { get; init; }
        public string Status { get; init; } = string.Empty;
        public DateTime InvitedAt { get; init; }
        public Guid? InvitedBy { get; init; }
        public DateTime? RespondedAt { get; init; }
        public Guid? RespondedBy { get; init; }

        public ClubLeagueMembership ToModel() => new()
        {
            Id = Id,
            ClubId = ClubId,
            LeagueId = LeagueId,
            Status = Enum.Parse<MembershipStatus>(Status),
            InvitedAt = InvitedAt,
            InvitedBy = InvitedBy,
            RespondedAt = RespondedAt,
            RespondedBy = RespondedBy,
        };
    }
}
