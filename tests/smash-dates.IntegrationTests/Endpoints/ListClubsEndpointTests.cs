using System.Net;
using System.Net.Http.Json;
using smash_dates.Endpoints.Clubs;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class ListClubsEndpointTests : IntegrationTestBase
{
    public ListClubsEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        var response = await Client.GetAsync("/api/clubs");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_ReturnsAll_SortedByName()
    {
        await Seeder.CreateClubAsync("Beta", "BETA");
        await Seeder.CreateClubAsync("Alpha", "ALPHA");
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.GetAsync("/api/clubs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListClubsEndpoint.ClubSummary[]>();
        body!.Select(c => c.Name).Should().ContainInOrder("Alpha", "Beta");
    }
}
