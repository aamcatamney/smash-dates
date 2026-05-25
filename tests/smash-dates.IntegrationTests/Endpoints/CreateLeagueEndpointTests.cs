using System.Net;
using System.Net.Http.Json;
using Dapper;
using Npgsql;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class CreateLeagueEndpointTests : IntegrationTestBase
{
    public CreateLeagueEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Post_AsSystemAdmin_CreatesLeague_Returns201()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("admin@example.com", "correct-horse-battery");
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsJsonAsync("/api/leagues", new
        {
            name = "North London",
            description = "Top division",
            firstLeagueAdminUserId = admin.Id,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().StartWith("/api/leagues/");
    }

    [Fact]
    public async Task Post_Anonymous_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/leagues", new { name = "X", firstLeagueAdminUserId = Guid.NewGuid() });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_AsNonAdmin_Returns403()
    {
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync("/api/leagues", new { name = "X", firstLeagueAdminUserId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_DuplicateName_Returns409()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("admin@example.com", "correct-horse-battery");
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "correct-horse-battery" });

        await Client.PostAsJsonAsync("/api/leagues", new { name = "North London", firstLeagueAdminUserId = admin.Id });

        var response = await Client.PostAsJsonAsync("/api/leagues", new { name = "north london", firstLeagueAdminUserId = admin.Id });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Post_EmptyName_Returns400()
    {
        await Client.LoginAsSystemAdminAsync("admin@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync("/api/leagues", new { name = "", firstLeagueAdminUserId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_FirstAdminUserUnknown_Returns400()
    {
        await Client.LoginAsSystemAdminAsync("admin@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync("/api/leagues", new
        {
            name = "North London",
            firstLeagueAdminUserId = Guid.NewGuid(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_CreatesInitialAdminGrant()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("admin@example.com", "correct-horse-battery");
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsJsonAsync("/api/leagues", new
        {
            name = "North London",
            firstLeagueAdminUserId = admin.Id,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        await using var conn = new NpgsqlConnection(Fixture.ConnectionString);
        await conn.OpenAsync();
        var count = await SqlMapper.ExecuteScalarAsync<int>(
            conn,
            "SELECT count(*) FROM league_admins WHERE user_id = @id",
            new { id = admin.Id });
        count.Should().Be(1);
    }
}
