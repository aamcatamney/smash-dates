using System.Net;
using System.Net.Http.Json;
using smash_dates.Endpoints.Divisions;
using smash_dates.IntegrationTests.Infrastructure;
using smash_dates.Models;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class ListDivisionsEndpointTests : IntegrationTestBase
{
    public ListDivisionsEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Get_ReturnsDivisionsForLeague_OrderedByGenderThenRank()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("admin@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        await Seeder.CreateDivisionAsync(leagueId, "Mens 2", DivisionGender.Mens, 2, 9);
        await Seeder.CreateDivisionAsync(leagueId, "Mens 1", DivisionGender.Mens, 1, 9);
        await Seeder.CreateDivisionAsync(leagueId, "Ladies 1", DivisionGender.Ladies, 1, 6);
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.GetAsync($"/api/leagues/{leagueId}/divisions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListDivisionsEndpoint.DivisionSummary[]>();
        body!.Select(s => s.Name).Should().ContainInOrder("Ladies 1", "Mens 1", "Mens 2");
    }

    [Fact]
    public async Task Get_UnknownLeague_Returns404()
    {
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);
        var response = await Client.GetAsync($"/api/leagues/{Guid.NewGuid()}/divisions");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
