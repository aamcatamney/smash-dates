using smash_dates.Models;

namespace smash_dates.Repositories;

// Read DTOs for the assembled board.
public sealed record BoardGamePlayer(Guid AttendanceId, string DisplayName, Gender Gender, int? Grade, GameSide Side);
public sealed record BoardGame(Guid Id, GameType Type, IReadOnlyList<BoardGamePlayer> Players);
public sealed record BoardCourt(Guid Id, string Label, BoardGame? ActiveGame);
public sealed record BoardAttendee(
    Guid Id, Guid? PlayerId, string DisplayName, Gender Gender, int? Grade,
    AttendanceStatus Status, DateTime WaitingSince,
    int GamesPlayed, int GamesWon);
public sealed record BoardView(
    PegboardSession Session,
    IReadOnlyList<BoardCourt> Courts,
    IReadOnlyList<BoardAttendee> Attendees,
    // Caller-scoped: true when the requester may run this session (SessionHost/ClubAdmin/SystemAdmin).
    // The repo always builds it false; the endpoint sets it from the principal. Drives the client's
    // host-vs-viewer chrome — viewers get a read-only board (see GetBoardEndpoint).
    bool CanManage = false);

// One attendee's makeup-relevant facts, used by the fill engine.
public sealed record WaitingAttendee(Guid Id, Gender Gender, int? Grade, DateTime WaitingSince, int GamesPlayed);

public interface IPegboardRepository
{
    // Sessions
    Task<PegboardSession?> GetSessionAsync(Guid sessionId, CancellationToken ct = default);
    Task<PegboardSession?> GetOpenByClubAsync(Guid clubId, CancellationToken ct = default);
    Task<IReadOnlyList<PegboardSession>> ListByClubAsync(Guid clubId, CancellationToken ct = default);
    Task<Guid> OpenAsync(Guid clubId, string name, Guid openedBy, CancellationToken ct = default);
    Task<bool> CloseAsync(Guid sessionId, CancellationToken ct = default);

    // Courts
    Task<PegboardCourt?> GetCourtAsync(Guid courtId, CancellationToken ct = default);
    Task<Guid> AddCourtAsync(Guid sessionId, string label, CancellationToken ct = default);
    Task<bool> HasActiveGameOnCourtAsync(Guid courtId, CancellationToken ct = default);
    Task<bool> RemoveCourtAsync(Guid courtId, CancellationToken ct = default);

    // Attendances
    Task<PegboardAttendance?> GetAttendanceAsync(Guid attendanceId, CancellationToken ct = default);
    Task<Guid> AddPlayerAttendanceAsync(Guid sessionId, Guid playerId, Gender gender, int? grade, CancellationToken ct = default);
    Task<Guid> AddGuestAttendanceAsync(Guid sessionId, string guestName, Gender gender, int? grade, CancellationToken ct = default);
    Task<bool> SetAttendanceStatusAsync(Guid attendanceId, AttendanceStatus status, CancellationToken ct = default);
    Task<bool> IsInActiveGameAsync(Guid attendanceId, CancellationToken ct = default);
    Task<bool> RemoveAttendanceAsync(Guid attendanceId, CancellationToken ct = default);
    Task<IReadOnlyList<WaitingAttendee>> ListWaitingAsync(Guid sessionId, CancellationToken ct = default);

    // Games
    Task<PegboardGame?> GetGameAsync(Guid gameId, CancellationToken ct = default);
    Task<Guid> StartGameAsync(Guid sessionId, Guid courtId, GameType type,
        IReadOnlyList<Guid> sideA, IReadOnlyList<Guid> sideB, CancellationToken ct = default);
    Task<bool> FinishGameAsync(Guid gameId, GameSide winnerSide, string? score, CancellationToken ct = default);
    Task<bool> CancelGameAsync(Guid gameId, CancellationToken ct = default);

    // Read model
    Task<BoardView?> GetBoardAsync(Guid sessionId, CancellationToken ct = default);

    // Unordered attendance-id pairs that have shared a finished game this session (for variety).
    Task<IReadOnlyList<(Guid A, Guid B)>> ListPlayedPairsAsync(Guid sessionId, CancellationToken ct = default);
}
