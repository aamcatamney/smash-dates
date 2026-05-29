using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class DeleteVenueEndpointTests : IntegrationTestBase
{
    public DeleteVenueEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Delete_AsClubAdmin_Returns204()
    {
        var admin = await Seeder.CreateUserAsync("admin@example.com", "correct-horse-battery");
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.GrantClubAdminAsync(clubId, admin.Id, admin.Id);
        var venueId = await Seeder.CreateVenueAsync(clubId, "Main Hall", 1);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "correct-horse-battery" });

        var response = await Client.DeleteAsync($"/api/clubs/{clubId}/venues/{venueId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_AsNonAdmin_Returns403()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        var venueId = await Seeder.CreateVenueAsync(clubId, "Main Hall", 1);
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.DeleteAsync($"/api/clubs/{clubId}/venues/{venueId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_UnknownVenue_Returns404()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);

        var response = await Client.DeleteAsync($"/api/clubs/{clubId}/venues/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
