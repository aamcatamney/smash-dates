using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class CreateLeagueEndpointTests : IntegrationTestBase
{
    public CreateLeagueEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Post_AsSystemAdmin_CreatesLeague_Returns201()
    {
        await Client.LoginAsSystemAdminAsync("admin@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync("/api/leagues", new
        {
            name = "North London",
            description = "Top division",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().StartWith("/api/leagues/");
    }

    [Fact]
    public async Task Post_Anonymous_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/leagues", new { name = "X" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_AsNonAdmin_Returns403()
    {
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync("/api/leagues", new { name = "X" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_DuplicateName_Returns409()
    {
        await Client.LoginAsSystemAdminAsync("admin@example.com", "correct-horse-battery", Seeder);
        await Client.PostAsJsonAsync("/api/leagues", new { name = "North London" });

        var response = await Client.PostAsJsonAsync("/api/leagues", new { name = "north london" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Post_EmptyName_Returns400()
    {
        await Client.LoginAsSystemAdminAsync("admin@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync("/api/leagues", new { name = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
