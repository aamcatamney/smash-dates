using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

// Scheduling a pegboard session ahead of time: create (Scheduled), open it later, edit/delete
// while scheduled, the one-Open-per-club rule, venue validation, and the board's club name.
public sealed class ScheduledSessionEndpointsTests : IntegrationTestBase
{
    public ScheduledSessionEndpointsTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record SessionRow(Guid Id, Guid ClubId, string Name, string Status);
    private sealed record ListRow(
        Guid Id, string Name, string Status, string? ScheduledDate, string? StartTime,
        int? DurationMinutes, Guid? VenueId, string? VenueName, string? VenueAddress, string? OpenedAt, string? ClosedAt);
    private sealed record BoardRow(BoardSession Session, bool CanManage, string ClubName, string ClubShortCode);
    private sealed record BoardSession(Guid Id, Guid ClubId, string Name, string Status);

    private async Task<Guid> LoginSysAdminAndClub()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);
        return clubId;
    }

    private static object SchedulePayload(string name, string date, string? time = null,
        int? duration = null, Guid? venueId = null) =>
        new { name, scheduledDate = date, startTime = time, durationMinutes = duration, venueId };

    [Fact]
    public async Task Schedule_ThenOpen_TransitionsToOpen_AndBoardShowsClubName()
    {
        var clubId = await LoginSysAdminAndClub();
        var venueId = await Seeder.CreateVenueAsync(clubId, "Main Hall");

        var created = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/pegboard/sessions/scheduled",
            SchedulePayload("Next Tuesday", "2026-06-16", "19:30:00", 120, venueId));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var session = (await created.Content.ReadFromJsonAsync<SessionRow>())!;
        session.Status.Should().Be("Scheduled");

        // It shows up in the list as Scheduled with the venue name resolved.
        var list = await Client.GetFromJsonAsync<List<ListRow>>($"/api/clubs/{clubId}/pegboard/sessions");
        var row = list!.Single(r => r.Id == session.Id);
        row.Status.Should().Be("Scheduled");
        row.VenueName.Should().Be("Main Hall");
        row.OpenedAt.Should().BeNull();

        // Open it: Scheduled -> Open.
        var opened = await Client.PostAsync($"/api/clubs/{clubId}/pegboard/sessions/{session.Id}/open", null);
        opened.StatusCode.Should().Be(HttpStatusCode.OK);
        (await opened.Content.ReadFromJsonAsync<SessionRow>())!.Status.Should().Be("Open");

        // The board header carries the club's name and short code.
        var board = await Client.GetFromJsonAsync<BoardRow>($"/api/clubs/{clubId}/pegboard/sessions/{session.Id}/board");
        board!.ClubName.Should().Be("Acme");
        board.ClubShortCode.Should().Be("ACME");
        board.Session.Status.Should().Be("Open");
    }

    [Fact]
    public async Task OpenScheduled_WhileAnotherOpen_Returns409()
    {
        var clubId = await LoginSysAdminAndClub();
        // One open session for tonight.
        await Client.PostAsJsonAsync($"/api/clubs/{clubId}/pegboard/sessions", new { name = "Tonight" });
        // A scheduled one for later.
        var created = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/pegboard/sessions/scheduled",
            SchedulePayload("Later", "2026-06-20"));
        var session = (await created.Content.ReadFromJsonAsync<SessionRow>())!;

        var open = await Client.PostAsync($"/api/clubs/{clubId}/pegboard/sessions/{session.Id}/open", null);
        open.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Schedule_WithoutDate_Returns400()
    {
        var clubId = await LoginSysAdminAndClub();
        var res = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/pegboard/sessions/scheduled",
            new { name = "No date", startTime = (string?)null, durationMinutes = (int?)null, venueId = (Guid?)null });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Schedule_WithVenueFromAnotherClub_Returns400()
    {
        var clubId = await LoginSysAdminAndClub();
        var otherClub = await Seeder.CreateClubAsync("Other", "OTHR");
        var otherVenue = await Seeder.CreateVenueAsync(otherClub, "Their Hall");

        var res = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/pegboard/sessions/scheduled",
            SchedulePayload("Wrong venue", "2026-06-16", venueId: otherVenue));
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EditAndDelete_OnlyWhileScheduled()
    {
        var clubId = await LoginSysAdminAndClub();
        var created = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/pegboard/sessions/scheduled",
            SchedulePayload("Draft", "2026-06-16"));
        var session = (await created.Content.ReadFromJsonAsync<SessionRow>())!;

        // Edit while scheduled.
        var edit = await Client.PatchAsJsonAsync($"/api/clubs/{clubId}/pegboard/sessions/{session.Id}",
            SchedulePayload("Renamed", "2026-06-17", "20:00:00"));
        edit.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var list = await Client.GetFromJsonAsync<List<ListRow>>($"/api/clubs/{clubId}/pegboard/sessions");
        list!.Single(r => r.Id == session.Id).Name.Should().Be("Renamed");

        // Open it, then editing/deleting is rejected.
        await Client.PostAsync($"/api/clubs/{clubId}/pegboard/sessions/{session.Id}/open", null);
        var editOpen = await Client.PatchAsJsonAsync($"/api/clubs/{clubId}/pegboard/sessions/{session.Id}",
            SchedulePayload("Nope", "2026-06-18"));
        editOpen.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var delOpen = await Client.DeleteAsync($"/api/clubs/{clubId}/pegboard/sessions/{session.Id}");
        delOpen.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeleteScheduled_RemovesIt()
    {
        var clubId = await LoginSysAdminAndClub();
        var created = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/pegboard/sessions/scheduled",
            SchedulePayload("Throwaway", "2026-06-16"));
        var session = (await created.Content.ReadFromJsonAsync<SessionRow>())!;

        var del = await Client.DeleteAsync($"/api/clubs/{clubId}/pegboard/sessions/{session.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await Client.GetFromJsonAsync<List<ListRow>>($"/api/clubs/{clubId}/pegboard/sessions");
        list!.Any(r => r.Id == session.Id).Should().BeFalse();
    }
}
