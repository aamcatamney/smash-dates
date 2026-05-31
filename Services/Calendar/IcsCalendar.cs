using System.Text;

namespace smash_dates.Services.Calendar;

// One all-day calendar event. Tentative maps to a TENTATIVE VEVENT status (used for
// not-yet-confirmed fixtures); otherwise CONFIRMED.
public sealed record IcsEvent(string Uid, DateOnly Date, string Summary, string Location, string Description, bool Tentative);

// Minimal RFC-5545 iCalendar writer (no dependency). All-day events (matches have a date but
// no kick-off time in the domain): DTSTART;VALUE=DATE plus an exclusive next-day DTEND. Lines
// use CRLF, text is escaped, and long lines are folded at 75 octets.
public static class IcsCalendar
{
    public static string Build(string calendarName, IReadOnlyList<IcsEvent> events, DateTime nowUtc)
    {
        var lines = new List<string>
        {
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            "PRODID:-//smash-dates//EN",
            "CALSCALE:GREGORIAN",
            "METHOD:PUBLISH",
            $"X-WR-CALNAME:{Escape(calendarName)}",
        };

        var stamp = nowUtc.ToString("yyyyMMdd'T'HHmmss'Z'");
        foreach (var e in events)
        {
            lines.Add("BEGIN:VEVENT");
            lines.Add($"UID:{Escape(e.Uid)}@smash-dates");
            lines.Add($"DTSTAMP:{stamp}");
            lines.Add($"DTSTART;VALUE=DATE:{e.Date:yyyyMMdd}");
            lines.Add($"DTEND;VALUE=DATE:{e.Date.AddDays(1):yyyyMMdd}");
            lines.Add($"SUMMARY:{Escape(e.Summary)}");
            if (e.Location.Length > 0) lines.Add($"LOCATION:{Escape(e.Location)}");
            if (e.Description.Length > 0) lines.Add($"DESCRIPTION:{Escape(e.Description)}");
            lines.Add($"STATUS:{(e.Tentative ? "TENTATIVE" : "CONFIRMED")}");
            lines.Add("END:VEVENT");
        }

        lines.Add("END:VCALENDAR");

        var sb = new StringBuilder();
        foreach (var line in lines)
        {
            foreach (var folded in Fold(line)) sb.Append(folded).Append("\r\n");
        }
        return sb.ToString();
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,")
             .Replace("\r\n", "\\n").Replace("\n", "\\n");

    // Fold a content line so no physical line exceeds 75 octets; continuations start with a space.
    private static IEnumerable<string> Fold(string line)
    {
        var bytes = Encoding.UTF8.GetBytes(line);
        if (bytes.Length <= 75)
        {
            yield return line;
            yield break;
        }

        var first = true;
        var i = 0;
        while (i < bytes.Length)
        {
            // Continuation lines carry a leading space, so they may hold one fewer content octet.
            var max = first ? 75 : 74;
            var take = Math.Min(max, bytes.Length - i);
            // Don't split a multi-byte UTF-8 sequence: back off until the next byte isn't a continuation byte.
            while (take > 0 && i + take < bytes.Length && (bytes[i + take] & 0xC0) == 0x80) take--;
            var chunk = Encoding.UTF8.GetString(bytes, i, take);
            yield return first ? chunk : " " + chunk;
            i += take;
            first = false;
        }
    }
}
