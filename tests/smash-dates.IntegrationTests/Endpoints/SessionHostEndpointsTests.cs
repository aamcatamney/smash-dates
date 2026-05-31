using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class SessionHostEndpointsTests : IntegrationTestBase
{
    public SessionHostEndpointsTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Grant_AsClubAdmin_ThenList_ShowsHost()
    {
        var admin = await Seeder.CreateUserAsync("admin@example.com", "correct-horse-battery");
        var host = await Seeder.CreateUserAsync("host@example.com", "correct-horse-battery");
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.GrantClubAdminAsync(clubId, admin.Id, admin.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "correct-horse-battery" });

        var grant = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/session-hosts", new { userId = host.Id });
        grant.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await Client.GetFromJsonAsync<List<HostRow>>($"/api/clubs/{clubId}/session-hosts");
        list!.Should().ContainSingle(h => h.UserId == host.Id);
    }

    [Fact]
    public async Task Grant_AsNonAdmin_Returns403()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.CreateUserAsync("nobody@example.com", "correct-horse-battery");
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "nobody@example.com", password = "correct-horse-battery" });

        var grant = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/session-hosts", new { userId = Guid.NewGuid() });
        grant.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private sealed record HostRow(Guid UserId, DateTime GrantedAt);
}
