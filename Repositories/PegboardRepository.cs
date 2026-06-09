using System.Data;
using Dapper;
using smash_dates.Data;
using smash_dates.Models;

namespace smash_dates.Repositories;

public sealed class PegboardRepository : IPegboardRepository
{
    private const string SessionCols =
        "id, club_id, name, status, opened_by, opened_at, closed_at, " +
        "scheduled_date, start_time, duration_minutes, venue_id";
    private readonly IDbConnectionFactory _factory;

    public PegboardRepository(IDbConnectionFactory factory) => _factory = factory;

    // ---- Sessions ----
    public async Task<PegboardSession?> GetSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<PegboardSession>(new CommandDefinition(
            $"SELECT {SessionCols} FROM pegboard_sessions WHERE id = @sessionId", new { sessionId }, cancellationToken: ct));
    }

    public async Task<PegboardSession?> GetOpenByClubAsync(Guid clubId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<PegboardSession>(new CommandDefinition(
            $"SELECT {SessionCols} FROM pegboard_sessions WHERE club_id = @clubId AND status = 'Open'",
            new { clubId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<SessionListRow>> ListByClubAsync(Guid clubId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        // Upcoming (Scheduled) first by soonest date, then live/past by most-recent open time.
        var rows = await conn.QueryAsync<SessionListRow>(new CommandDefinition(
            @"SELECT s.id, s.name, s.status,
                     s.scheduled_date, s.start_time, s.duration_minutes,
                     s.venue_id, v.name AS venue_name,
                     s.opened_at, s.closed_at
              FROM pegboard_sessions s
              LEFT JOIN venues v ON v.id = s.venue_id
              WHERE s.club_id = @clubId
              ORDER BY (s.status = 'Scheduled') DESC,
                       CASE WHEN s.status = 'Scheduled' THEN s.scheduled_date END ASC,
                       s.opened_at DESC NULLS LAST",
            new { clubId }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<Guid> OpenAsync(Guid clubId, string name, Guid openedBy, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        // opened_at no longer defaults at the column level (a Scheduled row leaves it null), so set it here.
        return await conn.ExecuteScalarAsync<Guid>(new CommandDefinition(
            @"INSERT INTO pegboard_sessions (club_id, name, status, opened_by, opened_at)
              VALUES (@clubId, @name, 'Open', @openedBy, now()) RETURNING id",
            new { clubId, name, openedBy }, cancellationToken: ct));
    }

    public async Task<Guid> ScheduleAsync(Guid clubId, string name, DateOnly scheduledDate, TimeOnly? startTime,
        int? durationMinutes, Guid? venueId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<Guid>(new CommandDefinition(
            @"INSERT INTO pegboard_sessions
                  (club_id, name, status, scheduled_date, start_time, duration_minutes, venue_id)
              VALUES (@clubId, @name, 'Scheduled', @scheduledDate, @startTime, @durationMinutes, @venueId)
              RETURNING id",
            new { clubId, name, scheduledDate, startTime, durationMinutes, venueId }, cancellationToken: ct));
    }

    public async Task<bool> OpenScheduledAsync(Guid sessionId, Guid openedBy, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        // Scheduled -> Open. The partial unique index throws 23505 if the club already has an Open one.
        var rows = await conn.ExecuteAsync(new CommandDefinition(
            @"UPDATE pegboard_sessions
              SET status = 'Open', opened_at = now(), opened_by = @openedBy
              WHERE id = @sessionId AND status = 'Scheduled'",
            new { sessionId, openedBy }, cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> UpdateScheduledAsync(Guid sessionId, string name, DateOnly scheduledDate, TimeOnly? startTime,
        int? durationMinutes, Guid? venueId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(new CommandDefinition(
            @"UPDATE pegboard_sessions
              SET name = @name, scheduled_date = @scheduledDate, start_time = @startTime,
                  duration_minutes = @durationMinutes, venue_id = @venueId
              WHERE id = @sessionId AND status = 'Scheduled'",
            new { sessionId, name, scheduledDate, startTime, durationMinutes, venueId }, cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> DeleteScheduledAsync(Guid sessionId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM pegboard_sessions WHERE id = @sessionId AND status = 'Scheduled'",
            new { sessionId }, cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> CloseAsync(Guid sessionId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await OpenConnAsync(conn, ct);
        using var tx = conn.BeginTransaction();
        // End any in-progress games with no result (cancelled), then close the session.
        await conn.ExecuteAsync(new CommandDefinition(
            @"UPDATE pegboard_games SET status = 'Cancelled', ended_at = now()
              WHERE session_id = @sessionId AND status = 'Active'",
            new { sessionId }, transaction: tx, cancellationToken: ct));
        var rows = await conn.ExecuteAsync(new CommandDefinition(
            @"UPDATE pegboard_sessions SET status = 'Closed', closed_at = now()
              WHERE id = @sessionId AND status = 'Open'",
            new { sessionId }, transaction: tx, cancellationToken: ct));
        tx.Commit();
        return rows > 0;
    }

    // ---- Courts ----
    public async Task<PegboardCourt?> GetCourtAsync(Guid courtId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<PegboardCourt>(new CommandDefinition(
            "SELECT id, session_id, label, created_at FROM pegboard_courts WHERE id = @courtId",
            new { courtId }, cancellationToken: ct));
    }

    public async Task<Guid> AddCourtAsync(Guid sessionId, string label, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<Guid>(new CommandDefinition(
            "INSERT INTO pegboard_courts (session_id, label) VALUES (@sessionId, @label) RETURNING id",
            new { sessionId, label }, cancellationToken: ct));
    }

    public async Task<bool> HasActiveGameOnCourtAsync(Guid courtId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM pegboard_games WHERE court_id = @courtId AND status = 'Active')",
            new { courtId }, cancellationToken: ct));
    }

    public async Task<bool> RemoveCourtAsync(Guid courtId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM pegboard_courts WHERE id = @courtId", new { courtId }, cancellationToken: ct));
        return rows > 0;
    }

    // ---- Attendances ----
    public async Task<PegboardAttendance?> GetAttendanceAsync(Guid attendanceId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<PegboardAttendance>(new CommandDefinition(
            @"SELECT id, session_id, player_id, guest_name, gender, grade, status, waiting_since, created_at
              FROM pegboard_attendances WHERE id = @attendanceId", new { attendanceId }, cancellationToken: ct));
    }

    public async Task<Guid> AddPlayerAttendanceAsync(Guid sessionId, Guid playerId, Gender gender, int? grade, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<Guid>(new CommandDefinition(
            @"INSERT INTO pegboard_attendances (session_id, player_id, gender, grade)
              VALUES (@sessionId, @playerId, @gender, @grade) RETURNING id",
            new { sessionId, playerId, gender = gender.ToString(), grade }, cancellationToken: ct));
    }

    public async Task<Guid> AddGuestAttendanceAsync(Guid sessionId, string guestName, Gender gender, int? grade, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<Guid>(new CommandDefinition(
            @"INSERT INTO pegboard_attendances (session_id, guest_name, gender, grade)
              VALUES (@sessionId, @guestName, @gender, @grade) RETURNING id",
            new { sessionId, guestName, gender = gender.ToString(), grade }, cancellationToken: ct));
    }

    public async Task<bool> SetAttendanceStatusAsync(Guid attendanceId, AttendanceStatus status, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        // Returning to the queue refreshes wait time so finished/rejoining players go to the tail.
        var rows = await conn.ExecuteAsync(new CommandDefinition(
            @"UPDATE pegboard_attendances
              SET status = @status,
                  waiting_since = CASE WHEN @status = 'Waiting' THEN now() ELSE waiting_since END
              WHERE id = @attendanceId",
            new { attendanceId, status = status.ToString() }, cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> IsInActiveGameAsync(Guid attendanceId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
            @"SELECT EXISTS (
                SELECT 1 FROM pegboard_game_players gp
                JOIN pegboard_games g ON g.id = gp.game_id
                WHERE gp.attendance_id = @attendanceId AND g.status = 'Active')",
            new { attendanceId }, cancellationToken: ct));
    }

    public async Task<bool> RemoveAttendanceAsync(Guid attendanceId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM pegboard_attendances WHERE id = @attendanceId", new { attendanceId }, cancellationToken: ct));
        return rows > 0;
    }

    public async Task<IReadOnlyList<WaitingAttendee>> ListWaitingAsync(Guid sessionId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<WaitingAttendee>(new CommandDefinition(
            @"SELECT a.id, a.gender, a.grade::int AS grade, a.waiting_since,
                     (SELECT count(*) FROM pegboard_game_players gp
                      JOIN pegboard_games g ON g.id = gp.game_id
                      WHERE gp.attendance_id = a.id AND g.status = 'Finished')::int AS games_played
              FROM pegboard_attendances a
              WHERE a.session_id = @sessionId AND a.status = 'Waiting'
              ORDER BY a.waiting_since",
            new { sessionId }, cancellationToken: ct));
        return rows.AsList();
    }

    // ---- Games ----
    public async Task<PegboardGame?> GetGameAsync(Guid gameId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<PegboardGame>(new CommandDefinition(
            @"SELECT id, session_id, court_id, type, status, winner_side, score, started_at, ended_at
              FROM pegboard_games WHERE id = @gameId", new { gameId }, cancellationToken: ct));
    }

    public async Task<Guid> StartGameAsync(Guid sessionId, Guid courtId, GameType type,
        IReadOnlyList<Guid> sideA, IReadOnlyList<Guid> sideB, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await OpenConnAsync(conn, ct);
        using var tx = conn.BeginTransaction();

        var gameId = await conn.ExecuteScalarAsync<Guid>(new CommandDefinition(
            @"INSERT INTO pegboard_games (session_id, court_id, type)
              VALUES (@sessionId, @courtId, @type) RETURNING id",
            new { sessionId, courtId, type = type.ToString() }, transaction: tx, cancellationToken: ct));

        foreach (var id in sideA)
            await conn.ExecuteAsync(new CommandDefinition(
                "INSERT INTO pegboard_game_players (game_id, attendance_id, side) VALUES (@gameId, @id, 'A')",
                new { gameId, id }, transaction: tx, cancellationToken: ct));
        foreach (var id in sideB)
            await conn.ExecuteAsync(new CommandDefinition(
                "INSERT INTO pegboard_game_players (game_id, attendance_id, side) VALUES (@gameId, @id, 'B')",
                new { gameId, id }, transaction: tx, cancellationToken: ct));

        var all = sideA.Concat(sideB).ToArray();
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE pegboard_attendances SET status = 'Playing' WHERE id = ANY(@all)",
            new { all }, transaction: tx, cancellationToken: ct));

        tx.Commit();
        return gameId;
    }

    public async Task<bool> FinishGameAsync(Guid gameId, GameSide winnerSide, string? score, CancellationToken ct = default)
        => await EndGameAsync(gameId, "Finished", winnerSide.ToString(), score, ct);

    public async Task<bool> CancelGameAsync(Guid gameId, CancellationToken ct = default)
        => await EndGameAsync(gameId, "Cancelled", null, null, ct);

    private async Task<bool> EndGameAsync(Guid gameId, string status, string? winnerSide, string? score, CancellationToken ct)
    {
        using var conn = _factory.Create();
        await OpenConnAsync(conn, ct);
        using var tx = conn.BeginTransaction();

        var rows = await conn.ExecuteAsync(new CommandDefinition(
            @"UPDATE pegboard_games
              SET status = @status, winner_side = @winnerSide, score = @score, ended_at = now()
              WHERE id = @gameId AND status = 'Active'",
            new { gameId, status, winnerSide, score }, transaction: tx, cancellationToken: ct));
        if (rows == 0) { tx.Rollback(); return false; }

        // Return the game's players to the queue tail.
        await conn.ExecuteAsync(new CommandDefinition(
            @"UPDATE pegboard_attendances SET status = 'Waiting', waiting_since = now()
              WHERE id IN (SELECT attendance_id FROM pegboard_game_players WHERE game_id = @gameId)
                AND status = 'Playing'",
            new { gameId }, transaction: tx, cancellationToken: ct));

        tx.Commit();
        return true;
    }

    // ---- Board read ----
    public async Task<BoardView?> GetBoardAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await GetSessionAsync(sessionId, ct);
        if (session is null) return null;

        using var conn = _factory.Create();

        var courts = (await conn.QueryAsync<CourtRow>(new CommandDefinition(
            "SELECT id, label FROM pegboard_courts WHERE session_id = @sessionId ORDER BY created_at",
            new { sessionId }, cancellationToken: ct))).AsList();

        var activeGames = (await conn.QueryAsync<ActiveGameRow>(new CommandDefinition(
            "SELECT id, court_id, type FROM pegboard_games WHERE session_id = @sessionId AND status = 'Active'",
            new { sessionId }, cancellationToken: ct))).AsList();

        var gamePlayers = (await conn.QueryAsync<GamePlayerRow>(new CommandDefinition(
            @"SELECT gp.game_id, gp.attendance_id, gp.side,
                     COALESCE(a.guest_name, p.full_name) AS display_name, a.gender, a.grade::int AS grade
              FROM pegboard_game_players gp
              JOIN pegboard_games g ON g.id = gp.game_id
              JOIN pegboard_attendances a ON a.id = gp.attendance_id
              LEFT JOIN players p ON p.id = a.player_id
              WHERE g.session_id = @sessionId AND g.status = 'Active'",
            new { sessionId }, cancellationToken: ct))).AsList();

        var attendees = (await conn.QueryAsync<AttendeeRow>(new CommandDefinition(
            @"SELECT a.id, a.player_id, COALESCE(a.guest_name, p.full_name) AS display_name,
                     a.gender, a.grade::int AS grade, a.status, a.waiting_since,
                     (SELECT count(*) FROM pegboard_game_players gp
                      JOIN pegboard_games g ON g.id = gp.game_id
                      WHERE gp.attendance_id = a.id AND g.status = 'Finished')::int AS games_played,
                     (SELECT count(*) FROM pegboard_game_players gp
                      JOIN pegboard_games g ON g.id = gp.game_id
                      WHERE gp.attendance_id = a.id AND g.status = 'Finished'
                        AND g.winner_side = gp.side)::int AS games_won
              FROM pegboard_attendances a
              LEFT JOIN players p ON p.id = a.player_id
              WHERE a.session_id = @sessionId
              ORDER BY a.waiting_since",
            new { sessionId }, cancellationToken: ct))).AsList();

        BoardGame? GameForCourt(Guid courtId)
        {
            var g = activeGames.FirstOrDefault(x => x.CourtId == courtId);
            if (g is null) return null;
            var players = gamePlayers.Where(p => p.GameId == g.Id)
                .Select(p => new BoardGamePlayer(p.AttendanceId, p.DisplayName,
                    Enum.Parse<Gender>(p.Gender), p.Grade, Enum.Parse<GameSide>(p.Side)))
                .ToList();
            return new BoardGame(g.Id, Enum.Parse<GameType>(g.Type), players);
        }

        var boardCourts = courts.Select(c => new BoardCourt(c.Id, c.Label, GameForCourt(c.Id))).ToList();
        var boardAttendees = attendees.Select(a => new BoardAttendee(
            a.Id, a.PlayerId, a.DisplayName, Enum.Parse<Gender>(a.Gender), a.Grade,
            Enum.Parse<AttendanceStatus>(a.Status), a.WaitingSince, a.GamesPlayed, a.GamesWon)).ToList();

        return new BoardView(session, boardCourts, boardAttendees);
    }

    public async Task<IReadOnlyList<(Guid A, Guid B)>> ListPlayedPairsAsync(Guid sessionId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<PairRow>(new CommandDefinition(
            @"SELECT gp1.attendance_id AS a, gp2.attendance_id AS b
              FROM pegboard_game_players gp1
              JOIN pegboard_game_players gp2 ON gp1.game_id = gp2.game_id AND gp1.attendance_id < gp2.attendance_id
              JOIN pegboard_games g ON g.id = gp1.game_id
              WHERE g.session_id = @sessionId AND g.status = 'Finished'",
            new { sessionId }, cancellationToken: ct));
        return rows.Select(r => (r.A, r.B)).ToList();
    }

    private static async Task OpenConnAsync(IDbConnection conn, CancellationToken ct)
    {
        if (conn is System.Data.Common.DbConnection db) await db.OpenAsync(ct);
        else conn.Open();
    }

    // Private row types for the board read (snake_case columns -> PascalCase props via Dapper).
    private sealed record CourtRow(Guid Id, string Label);
    private sealed record ActiveGameRow(Guid Id, Guid CourtId, string Type);
    private sealed record GamePlayerRow(Guid GameId, Guid AttendanceId, string Side, string DisplayName, string Gender, int? Grade);
    private sealed record AttendeeRow(
        Guid Id, Guid? PlayerId, string DisplayName, string Gender, int? Grade,
        string Status, DateTime WaitingSince, int GamesPlayed, int GamesWon);
    private sealed record PairRow(Guid A, Guid B);
}
