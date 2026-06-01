using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

// The board read is open to any authenticated user, but it reports `canManage` so the client
// can render a read-only board for viewers and host controls for runners.
public sealed class PegboardBoardCanManageTests : IntegrationTestBase
{
    public PegboardBoardCanManageTests(PostgresFixture fixture) : base(fixture) { }

    private async Task<(Guid ClubId, Guid SessionId)> OpenSessionAsSysAdmin()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);
        var open = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/pegboard/sessions", new { name = "Tuesday" });
        var session = await open.Content.ReadFromJsonAsync<SessionRow>();
        return (clubId, session!.Id);
    }

    [Fact]
    public async Task GetBoard_AsSystemAdmin_CanManageTrue()
    {
        var (clubId, sessionId) = await OpenSessionAsSysAdmin();

        var board = await Client.GetFromJsonAsync<BoardDto>(
            $"/api/clubs/{clubId}/pegboard/sessions/{sessionId}/board");

        board!.CanManage.Should().BeTrue();
    }

    [Fact]
    public async Task GetBoard_AsViewer_Returns200_CanManageFalse()
    {
        var (clubId, sessionId) = await OpenSessionAsSysAdmin();

        // A plain authenticated user with no role on the club: a viewer.
        await Client.LoginAsAsync("viewer@example.com", "correct-horse-battery", Seeder);

        var response = await Client.GetAsync($"/api/clubs/{clubId}/pegboard/sessions/{sessionId}/board");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var board = await response.Content.ReadFromJsonAsync<BoardDto>();
        board!.CanManage.Should().BeFalse();
    }

    [Fact]
    public async Task GetBoard_AsSessionHost_CanManageTrue()
    {
        var admin = await Seeder.CreateUserAsync("admin@example.com", "correct-horse-battery");
        var host = await Seeder.CreateUserAsync("host@example.com", "correct-horse-battery");
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.GrantClubAdminAsync(clubId, admin.Id, admin.Id);

        await Client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "correct-horse-battery" });
        var open = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/pegboard/sessions", new { name = "Tuesday" });
        var session = await open.Content.ReadFromJsonAsync<SessionRow>();
        await Client.PostAsJsonAsync($"/api/clubs/{clubId}/session-hosts", new { userId = host.Id });

        // Re-auth as the granted host.
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "host@example.com", password = "correct-horse-battery" });

        var board = await Client.GetFromJsonAsync<BoardDto>(
            $"/api/clubs/{clubId}/pegboard/sessions/{session!.Id}/board");

        board!.CanManage.Should().BeTrue();
    }

    private sealed record SessionRow(Guid Id, Guid ClubId, string Name, string Status);
    private sealed record BoardDto(bool CanManage);
}
