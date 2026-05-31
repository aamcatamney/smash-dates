using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class PegboardCourtAttendanceEndpointsTests : IntegrationTestBase
{
    public PegboardCourtAttendanceEndpointsTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task AddCourt_Returns201()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);
        var open = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/pegboard/sessions", new { name = "Tue" });
        var sid = (await open.Content.ReadFromJsonAsync<SessionRow>())!.Id;

        var add = await Client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/pegboard/sessions/{sid}/courts", new { label = "Court 1" });

        add.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task AddGuestAttendance_Returns201()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);
        var open = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/pegboard/sessions", new { name = "Tue" });
        var sid = (await open.Content.ReadFromJsonAsync<SessionRow>())!.Id;

        var add = await Client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/pegboard/sessions/{sid}/attendances",
            new { guestName = "Alice", gender = "Female", grade = 2 });

        add.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task SetAttendanceStatus_ToPlaying_Returns400()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);
        var open = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/pegboard/sessions", new { name = "Tue" });
        var sid = (await open.Content.ReadFromJsonAsync<SessionRow>())!.Id;

        var add = await Client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/pegboard/sessions/{sid}/attendances",
            new { guestName = "Alice", gender = "Female" });
        add.StatusCode.Should().Be(HttpStatusCode.Created);
        var attendanceId = (await add.Content.ReadFromJsonAsync<AttendanceRow>())!.Id;

        var set = await Client.PatchAsJsonAsync(
            $"/api/clubs/{clubId}/pegboard/sessions/{sid}/attendances/{attendanceId}",
            new { status = "Playing" });

        set.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record SessionRow(Guid Id, Guid ClubId, string Name, string Status);
    private sealed record AttendanceRow(Guid Id);
}
