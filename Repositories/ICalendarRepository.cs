namespace smash_dates.Repositories;

// A scheduled match with the names needed for a calendar event. Status is the raw text
// (Proposed/Confirmed/Played).
public sealed record CalendarMatch(
    Guid Id,
    DateOnly MatchDate,
    string Status,
    string HomeTeamName,
    string AwayTeamName,
    string VenueName,
    string DivisionName,
    string LeagueName);

public interface ICalendarRepository
{
    Task<IReadOnlyList<CalendarMatch>> ListByClubAsync(Guid clubId, CancellationToken ct = default);
    Task<IReadOnlyList<CalendarMatch>> ListByLeagueAsync(Guid leagueId, CancellationToken ct = default);
    Task<IReadOnlyList<CalendarMatch>> ListByTeamAsync(Guid teamId, CancellationToken ct = default);
}
