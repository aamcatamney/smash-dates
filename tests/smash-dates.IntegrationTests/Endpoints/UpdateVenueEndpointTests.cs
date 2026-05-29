using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class UpdateVenueEndpointTests : IntegrationTestBase
{
    public UpdateVenueEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private static HttpRequestMessage Patch(string url, object body) =>
        new(HttpMethod.Patch, url) { Content = JsonContent.Create(body) };

    [Fact]
    public async Task Patch_AsClubAdmin_UpdatesNameAndCapacity_Returns204()
    {
        var admin = await Seeder.CreateUserAsync("admin@example.com", "correct-horse-battery");
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.GrantClubAdminAsync(clubId, admin.Id, admin.Id);
        var venueId = await Seeder.CreateVenueAsync(clubId, "Main Hall", 1);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "correct-horse-battery" });

        var response = await Client.SendAsync(Patch($"/api/clubs/{clubId}/venues/{venueId}", new { name = "Main Court", capacity = 2 }));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Patch_InvalidCapacity_Returns400()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        var venueId = await Seeder.CreateVenueAsync(clubId, "Main Hall", 1);
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);

        var response = await Client.SendAsync(Patch($"/api/clubs/{clubId}/venues/{venueId}", new { name = "Main Hall", capacity = 5 }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Patch_AsNonAdmin_Returns403()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        var venueId = await Seeder.CreateVenueAsync(clubId, "Main Hall", 1);
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.SendAsync(Patch($"/api/clubs/{clubId}/venues/{venueId}", new { name = "X", capacity = 1 }));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Patch_VenueOfDifferentClub_Returns404()
    {
        var clubA = await Seeder.CreateClubAsync("Acme", "ACME");
        var clubB = await Seeder.CreateClubAsync("Beta", "BETA");
        var venueInB = await Seeder.CreateVenueAsync(clubB, "Beta Hall", 1);
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);

        var response = await Client.SendAsync(Patch($"/api/clubs/{clubA}/venues/{venueInB}", new { name = "X", capacity = 1 }));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
