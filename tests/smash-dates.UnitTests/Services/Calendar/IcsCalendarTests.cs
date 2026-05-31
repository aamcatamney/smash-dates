using smash_dates.Services.Calendar;

namespace smash_dates.UnitTests.Services.Calendar;

public sealed class IcsCalendarTests
{
    private static readonly DateTime Stamp = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static string OneEvent(IcsEvent e) => IcsCalendar.Build("Riverside fixtures", [e], Stamp);

    [Fact]
    public void Build_WrapsCalendarWithRequiredHeaders()
    {
        var ics = IcsCalendar.Build("Riverside fixtures", [], Stamp);

        ics.Should().StartWith("BEGIN:VCALENDAR\r\n");
        ics.Should().EndWith("END:VCALENDAR\r\n");
        ics.Should().Contain("VERSION:2.0");
        ics.Should().Contain("X-WR-CALNAME:Riverside fixtures");
        ics.Should().Contain("\r\n"); // CRLF line endings
    }

    [Fact]
    public void Build_AllDayEvent_HasDateStartAndExclusiveNextDayEnd()
    {
        var ics = OneEvent(new IcsEvent("m1", new DateOnly(2025, 9, 13), "RIV v TVB", "Hall", "Mens Div 1", false));

        ics.Should().Contain("BEGIN:VEVENT");
        ics.Should().Contain("UID:m1");
        ics.Should().Contain("DTSTAMP:20260101T120000Z");
        ics.Should().Contain("DTSTART;VALUE=DATE:20250913");
        ics.Should().Contain("DTEND;VALUE=DATE:20250914");
        ics.Should().Contain("LOCATION:Hall");
        ics.Should().Contain("STATUS:CONFIRMED");
        ics.Should().Contain("END:VEVENT");
    }

    [Fact]
    public void Build_TentativeEvent_MapsToTentativeStatus()
    {
        var ics = OneEvent(new IcsEvent("m1", new DateOnly(2025, 9, 13), "RIV v TVB", "Hall", "d", true));

        ics.Should().Contain("STATUS:TENTATIVE");
    }

    [Fact]
    public void Build_EscapesCommasSemicolonsAndBackslashes()
    {
        var ics = OneEvent(new IcsEvent("m1", new DateOnly(2025, 9, 13), "A, B; C\\D", "L", "d", false));

        ics.Should().Contain("SUMMARY:A\\, B\\; C\\\\D");
    }

    [Fact]
    public void Build_FoldsLongLinesToAtMost75Octets()
    {
        var longSummary = new string('x', 200);
        var ics = OneEvent(new IcsEvent("m1", new DateOnly(2025, 9, 13), longSummary, "L", "d", false));

        foreach (var line in ics.Split("\r\n"))
            System.Text.Encoding.UTF8.GetByteCount(line).Should().BeLessThanOrEqualTo(75);
    }
}
