using Microsoft.AspNetCore.DataProtection;
using smash_dates.Repositories;
using smash_dates.Services.Calendar;

namespace smash_dates.Endpoints.Calendar;

// Subscribable iCalendar fixture feeds. The .ics endpoint is anonymous (calendar apps fetch
// it server-side with no cookie) but authorised by an opaque Data-Protection token that
// encodes the scope + id. Authenticated "url" endpoints mint that token for the UI.
public static class CalendarEndpoints
{
    private const string Purpose = "smash-dates.calendar-feed.v1";

    public static IEndpointRouteBuilder MapCalendarEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/calendar/{token}.ics", Feed); // anonymous, token-authorised

        var mint = app.MapGroup("/api/calendar").RequireAuthorization();
        mint.MapGet("/club/{clubId:guid}/url", (Guid clubId, IDataProtectionProvider dp) => Mint(dp, "club", clubId));
        mint.MapGet("/league/{leagueId:guid}/url", (Guid leagueId, IDataProtectionProvider dp) => Mint(dp, "league", leagueId));
        mint.MapGet("/team/{teamId:guid}/url", (Guid teamId, IDataProtectionProvider dp) => Mint(dp, "team", teamId));
        return app;
    }

    private static IResult Mint(IDataProtectionProvider dp, string scope, Guid id)
    {
        var token = dp.CreateProtector(Purpose).Protect($"{scope}:{id}");
        return Results.Ok(new { url = $"/api/calendar/{token}.ics" });
    }

    private static async Task<IResult> Feed(
        string token,
        IDataProtectionProvider dp,
        ICalendarRepository calendar,
        IClubRepository clubs,
        ILeagueRepository leagues,
        ITeamRepository teams,
        CancellationToken ct)
    {
        string payload;
        try
        {
            payload = dp.CreateProtector(Purpose).Unprotect(token);
        }
        catch
        {
            return Results.NotFound();
        }

        var parts = payload.Split(':', 2);
        if (parts.Length != 2 || !Guid.TryParse(parts[1], out var id)) return Results.NotFound();

        IReadOnlyList<CalendarMatch> matches;
        string name;
        switch (parts[0])
        {
            case "club":
                if (await clubs.GetByIdAsync(id, ct) is not { } club) return Results.NotFound();
                matches = await calendar.ListByClubAsync(id, ct);
                name = $"{club.Name} fixtures";
                break;
            case "league":
                if (await leagues.GetByIdAsync(id, ct) is not { } league) return Results.NotFound();
                matches = await calendar.ListByLeagueAsync(id, ct);
                name = $"{league.Name} fixtures";
                break;
            case "team":
                if (await teams.GetByIdAsync(id, ct) is not { } team) return Results.NotFound();
                matches = await calendar.ListByTeamAsync(id, ct);
                name = $"{team.Name} fixtures";
                break;
            default:
                return Results.NotFound();
        }

        var events = matches.Select(m => new IcsEvent(
            m.Id.ToString(),
            m.MatchDate,
            $"{m.HomeTeamName} v {m.AwayTeamName} — {m.DivisionName} [{m.Status}]",
            m.VenueName,
            $"{m.LeagueName} · {m.Status}",
            m.Status == "Proposed")).ToList();

        var ics = IcsCalendar.Build(name, events, DateTime.UtcNow);
        return Results.Text(ics, "text/calendar; charset=utf-8");
    }
}
