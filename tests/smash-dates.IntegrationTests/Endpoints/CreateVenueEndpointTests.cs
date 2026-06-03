using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class CreateVenueEndpointTests : IntegrationTestBase
{
    public CreateVenueEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Post_AsClubAdmin_CreatesVenue_Returns201()
    {
        var admin = await Seeder.CreateUserAsync("admin@example.com", "correct-horse-battery");
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.GrantClubAdminAsync(clubId, admin.Id, admin.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/venues", new { name = "Main Hall", courts = 4, maxConcurrentMatches = 2 });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_DefaultsCourtsAndMaxConcurrent_Returns201()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/venues", new { name = "Annex" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_InvalidMaxConcurrent_Returns400()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/venues", new { name = "Big Hall", courts = 6, maxConcurrentMatches = 3 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_ZeroCourts_Returns400()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/venues", new { name = "No Courts", courts = 0, maxConcurrentMatches = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_UnknownClub_Returns404()
    {
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync(
            $"/api/clubs/{Guid.NewGuid()}/venues", new { name = "Hall", courts = 2, maxConcurrentMatches = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_DuplicateNameInClub_Returns409()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);
        await Client.PostAsJsonAsync($"/api/clubs/{clubId}/venues", new { name = "Main Hall", courts = 2, maxConcurrentMatches = 1 });

        var dup = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/venues", new { name = "main hall", courts = 4, maxConcurrentMatches = 2 });

        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Post_AsNonAdmin_Returns403()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/venues", new { name = "Hall", courts = 2, maxConcurrentMatches = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
