using System.Net;
using System.Net.Http.Json;
using smash_dates.Endpoints.Leagues;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class GetLeagueEndpointTests : IntegrationTestBase
{
    public GetLeagueEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Get_ExistingLeague_Returns200()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("admin@example.com", "correct-horse-battery");
        var id = await Seeder.CreateLeagueAsync("North London", admin.Id, "desc");
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.GetAsync($"/api/leagues/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetLeagueEndpoint.LeagueDetail>();
        body!.Name.Should().Be("North London");
        body.Description.Should().Be("desc");
    }

    [Fact]
    public async Task Get_MissingLeague_Returns404()
    {
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);
        var response = await Client.GetAsync($"/api/leagues/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
