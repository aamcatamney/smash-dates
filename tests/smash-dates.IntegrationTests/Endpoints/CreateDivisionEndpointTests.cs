using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class CreateDivisionEndpointTests : IntegrationTestBase
{
    public CreateDivisionEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Post_AsSystemAdmin_CreatesDivision_Returns201()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("admin@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsJsonAsync($"/api/leagues/{leagueId}/divisions", new
        {
            name = "Mens 1",
            gender = "Mens",
            rank = 1,
            rubbersPerMatch = 9,
            winPoints = 2,
            drawPoints = 1,
            lossPoints = 0,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_UnknownLeague_Returns404()
    {
        await Client.LoginAsSystemAdminAsync("admin@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync($"/api/leagues/{Guid.NewGuid()}/divisions", new
        {
            name = "Mens 1", gender = "Mens", rank = 1, rubbersPerMatch = 9, winPoints = 2, drawPoints = 1, lossPoints = 0,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_InvalidGender_Returns400()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("admin@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsJsonAsync($"/api/leagues/{leagueId}/divisions", new
        {
            name = "Mens 1", gender = "Vegan", rank = 1, rubbersPerMatch = 9, winPoints = 2, drawPoints = 1, lossPoints = 0,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_AsNonAdmin_Returns403()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("admin@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync($"/api/leagues/{leagueId}/divisions", new
        {
            name = "Mens 1", gender = "Mens", rank = 1, rubbersPerMatch = 9, winPoints = 2, drawPoints = 1, lossPoints = 0,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
