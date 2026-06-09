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

    [Fact]
    public async Task AddNewVisitor_CreatesRealVisitorPlayer_AndAddsThem()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);
        var open = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/pegboard/sessions", new { name = "Tue" });
        var sid = (await open.Content.ReadFromJsonAsync<SessionRow>())!.Id;

        var add = await Client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/pegboard/sessions/{sid}/attendances",
            new { newVisitor = new { fullName = "Vera Visitor", gender = "Female", grade = 3 } });
        add.StatusCode.Should().Be(HttpStatusCode.Created);

        // A real Player + Visitor link now exists on the club roster.
        var roster = await Client.GetFromJsonAsync<List<RosterRow>>($"/api/clubs/{clubId}/players");
        roster!.Should().ContainSingle(p => p.FullName == "Vera Visitor" && p.Type == "Visitor");
    }

    [Fact]
    public async Task AddNewVisitor_AsSessionHost_Succeeds()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        // A SessionHost is NOT a club admin, yet may register a walk-in visitor on the night.
        var host = await Seeder.CreateUserAsync("host@example.com", "correct-horse-battery");
        await Seeder.GrantSessionHostAsync(clubId, host.Id);
        (await Client.PostAsJsonAsync("/api/auth/login",
            new { email = "host@example.com", password = "correct-horse-battery" })).EnsureSuccessStatusCode();

        var open = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/pegboard/sessions", new { name = "Tue" });
        var sid = (await open.Content.ReadFromJsonAsync<SessionRow>())!.Id;

        var add = await Client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/pegboard/sessions/{sid}/attendances",
            new { newVisitor = new { fullName = "Walk In", gender = "Male", grade = (int?)null } });

        add.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task AddExistingPlayer_ById_Returns201()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);
        // Create a roster player via the club-players endpoint.
        var created = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/players",
            new { fullName = "Mara Member", gender = "Female", type = "Member" });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var playerId = (await created.Content.ReadFromJsonAsync<RosterRow>())!.Id;

        var open = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/pegboard/sessions", new { name = "Tue" });
        var sid = (await open.Content.ReadFromJsonAsync<SessionRow>())!.Id;

        var add = await Client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/pegboard/sessions/{sid}/attendances", new { playerId });
        add.StatusCode.Should().Be(HttpStatusCode.Created);

        // Adding the same player again is a 409.
        var again = await Client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/pegboard/sessions/{sid}/attendances", new { playerId });
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private sealed record SessionRow(Guid Id, Guid ClubId, string Name, string Status);
    private sealed record AttendanceRow(Guid Id);
    private sealed record RosterRow(Guid Id, Guid PlayerId, string FullName, string Gender, string Type);
}
