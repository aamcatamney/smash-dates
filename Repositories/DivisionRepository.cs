using Dapper;
using smash_dates.Data;
using smash_dates.Models;

namespace smash_dates.Repositories;

public sealed class DivisionRepository : IDivisionRepository
{
    private const string SelectColumns =
        "id, league_id, name, gender, rank, rubbers_per_match, win_points, draw_points, loss_points, created_at, updated_at";

    private readonly IDbConnectionFactory _factory;

    public DivisionRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<Division?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var row = await conn.QuerySingleOrDefaultAsync<DivisionRow>(
            new CommandDefinition(
                $"SELECT {SelectColumns} FROM divisions WHERE id = @id",
                new { id },
                cancellationToken: ct));
        return row?.ToDivision();
    }

    public async Task<IReadOnlyList<Division>> ListByLeagueAsync(Guid leagueId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<DivisionRow>(
            new CommandDefinition(
                $@"SELECT {SelectColumns} FROM divisions
                   WHERE league_id = @leagueId
                   ORDER BY gender, rank",
                new { leagueId },
                cancellationToken: ct));
        return rows.Select(r => r.ToDivision()).ToList();
    }

    public async Task<Guid> CreateAsync(
        Guid leagueId,
        string name,
        DivisionGender gender,
        int rank,
        int rubbersPerMatch,
        int winPoints,
        int drawPoints,
        int lossPoints,
        CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<Guid>(
            new CommandDefinition(
                @"INSERT INTO divisions
                  (league_id, name, gender, rank, rubbers_per_match, win_points, draw_points, loss_points)
                  VALUES (@leagueId, @name, @gender, @rank, @rubbersPerMatch, @winPoints, @drawPoints, @lossPoints)
                  RETURNING id",
                new
                {
                    leagueId,
                    name,
                    gender = gender.ToString(),
                    rank,
                    rubbersPerMatch,
                    winPoints,
                    drawPoints,
                    lossPoints,
                },
                cancellationToken: ct));
    }

    private sealed class DivisionRow
    {
        public Guid Id { get; init; }
        public Guid LeagueId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Gender { get; init; } = string.Empty;
        public int Rank { get; init; }
        public int RubbersPerMatch { get; init; }
        public int WinPoints { get; init; }
        public int DrawPoints { get; init; }
        public int LossPoints { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }

        public Division ToDivision() => new()
        {
            Id = Id,
            LeagueId = LeagueId,
            Name = Name,
            Gender = Enum.Parse<DivisionGender>(Gender),
            Rank = Rank,
            RubbersPerMatch = RubbersPerMatch,
            WinPoints = WinPoints,
            DrawPoints = DrawPoints,
            LossPoints = LossPoints,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
        };
    }
}
