using System.Net;
using System.Net.Http.Json;
using smash_dates.Endpoints.LeagueAdmins;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class ListLeagueAdminsEndpointTests : IntegrationTestBase
{
    public ListLeagueAdminsEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        var response = await Client.GetAsync($"/api/leagues/{Guid.NewGuid()}/admins");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_UnknownLeague_Returns404()
    {
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);
        var response = await Client.GetAsync($"/api/leagues/{Guid.NewGuid()}/admins");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_ReturnsCurrentAdmins()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var second = await Seeder.CreateUserAsync("second@example.com", "correct-horse-battery", displayName: "Second");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        await Seeder.GrantLeagueAdminAsync(leagueId, sys.Id, sys.Id);
        await Seeder.GrantLeagueAdminAsync(leagueId, second.Id, sys.Id);
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.GetAsync($"/api/leagues/{leagueId}/admins");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListLeagueAdminsEndpoint.LeagueAdminSummary[]>();
        body!.Select(a => a.Email).Should().BeEquivalentTo(new[] { "sys@example.com", "second@example.com" });
        body!.Should().Contain(a => a.DisplayName == "Second");
    }
}
