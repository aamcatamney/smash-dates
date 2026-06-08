using System.Net;
using System.Net.Http.Json;
using smash_dates.Endpoints.Auth;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

// Read-only "my role grants" view backing the profile page.
public sealed class MyGrantsEndpointTests : IntegrationTestBase
{
    public MyGrantsEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private const string Pwd = "correct-horse-battery";

    private async Task LoginAsync(string email) =>
        (await Client.PostAsJsonAsync("/api/auth/login", new { email, password = Pwd })).EnsureSuccessStatusCode();

    [Fact]
    public async Task Grants_Anonymous_Returns401()
    {
        var response = await Client.GetAsync("/api/auth/me/grants");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Grants_ListsEveryGrantType_NamingTheLeagueAndClubs()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", Pwd);
        var user = await Seeder.CreateUserAsync("holder@example.com", Pwd, displayName: "Holder");
        var leagueId = await Seeder.CreateLeagueAsync("North League", sys.Id);
        var adminClubId = await Seeder.CreateClubAsync("Acme", "ACME", contactEmail: "a@test");
        var hostClubId = await Seeder.CreateClubAsync("Beta", "BETA", contactEmail: "b@test");
        await Seeder.GrantLeagueAdminAsync(leagueId, user.Id, sys.Id);
        await Seeder.GrantClubAdminAsync(adminClubId, user.Id, sys.Id);
        await Seeder.GrantSessionHostAsync(hostClubId, user.Id, sys.Id);
        await LoginAsync("holder@example.com");

        var grants = await Client.GetFromJsonAsync<MyGrantsEndpoint.GrantsResponse>("/api/auth/me/grants");

        grants!.SystemAdmin.Should().BeFalse();
        grants.LeagueAdmin.Should().ContainSingle(g => g.Id == leagueId && g.Name == "North League");
        grants.ClubAdmin.Should().ContainSingle(g => g.Id == adminClubId && g.Name == "Acme");
        grants.SessionHost.Should().ContainSingle(g => g.Id == hostClubId && g.Name == "Beta");
    }

    [Fact]
    public async Task Grants_SystemAdmin_IsFlaggedTrue()
    {
        await Seeder.CreateSystemAdminUserAsync("boss@example.com", Pwd);
        await LoginAsync("boss@example.com");

        var grants = await Client.GetFromJsonAsync<MyGrantsEndpoint.GrantsResponse>("/api/auth/me/grants");

        grants!.SystemAdmin.Should().BeTrue();
    }

    [Fact]
    public async Task Grants_WithNoGrants_ReturnsEmptyLists()
    {
        await Seeder.CreateUserAsync("plain@example.com", Pwd);
        await LoginAsync("plain@example.com");

        var grants = await Client.GetFromJsonAsync<MyGrantsEndpoint.GrantsResponse>("/api/auth/me/grants");

        grants!.SystemAdmin.Should().BeFalse();
        grants.LeagueAdmin.Should().BeEmpty();
        grants.ClubAdmin.Should().BeEmpty();
        grants.SessionHost.Should().BeEmpty();
    }
}
