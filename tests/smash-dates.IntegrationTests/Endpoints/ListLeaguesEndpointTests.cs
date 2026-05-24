using System.Net;
using System.Net.Http.Json;
using smash_dates.Endpoints.Leagues;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class ListLeaguesEndpointTests : IntegrationTestBase
{
    public ListLeaguesEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        var response = await Client.GetAsync("/api/leagues");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Authenticated_ReturnsLeaguesSortedByName()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("admin@example.com", "correct-horse-battery");
        await Seeder.CreateLeagueAsync("Beta", admin.Id);
        await Seeder.CreateLeagueAsync("Alpha", admin.Id);
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.GetAsync("/api/leagues");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListLeaguesEndpoint.LeagueSummary[]>();
        body!.Select(s => s.Name).Should().ContainInOrder("Alpha", "Beta");
    }
}
