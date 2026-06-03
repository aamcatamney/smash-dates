using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class ListVenuesEndpointTests : IntegrationTestBase
{
    public ListVenuesEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record VenueDto(Guid Id, Guid ClubId, string Name, int Courts, int MaxConcurrentMatches);

    [Fact]
    public async Task Get_ReturnsVenuesForClub_AnyAuthenticatedUser()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.CreateVenueAsync(clubId, "Main Hall", 2);
        await Seeder.CreateVenueAsync(clubId, "Annex", 1);
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var venues = await Client.GetFromJsonAsync<VenueDto[]>($"/api/clubs/{clubId}/venues");

        venues.Should().NotBeNull();
        venues!.Length.Should().Be(2);
        // The seeder maps legacy capacity → courts = capacity * 2.
        venues.Single(v => v.Name == "Main Hall").Courts.Should().Be(4);
        venues.Single(v => v.Name == "Main Hall").MaxConcurrentMatches.Should().Be(2);
    }

    [Fact]
    public async Task Get_Unauthenticated_Returns401()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");

        var response = await Client.GetAsync($"/api/clubs/{clubId}/venues");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
