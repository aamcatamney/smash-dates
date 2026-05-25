using System.Net;
using System.Net.Http.Json;
using smash_dates.Endpoints.Clubs;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class GetClubEndpointTests : IntegrationTestBase
{
    public GetClubEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Get_Existing_ReturnsClub()
    {
        var id = await Seeder.CreateClubAsync("Acme", "ACME", "contact@acme.test", "notes here");
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.GetAsync($"/api/clubs/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetClubEndpoint.ClubDetail>();
        body!.ShortCode.Should().Be("ACME");
        body.ContactEmail.Should().Be("contact@acme.test");
    }

    [Fact]
    public async Task Get_Missing_Returns404()
    {
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);
        var response = await Client.GetAsync($"/api/clubs/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
