using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class CreateSeasonEndpointTests : IntegrationTestBase
{
    public CreateSeasonEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private static object Week(string start, string end, string type) =>
        new { startDate = start, endDate = end, weekType = type };

    [Fact]
    public async Task Post_AsSystemAdmin_WithWeeks_Returns201()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsJsonAsync($"/api/leagues/{leagueId}/seasons", new
        {
            name = "2025/26",
            startDate = "2025-09-01",
            endDate = "2026-04-30",
            weeks = new[]
            {
                Week("2025-09-01", "2025-09-07", "Level"),
                Week("2025-09-08", "2025-09-14", "Mixed"),
            },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_AsLeagueAdmin_Returns201()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var la = await Seeder.CreateUserAsync("la@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        await Seeder.GrantLeagueAdminAsync(leagueId, la.Id, sys.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "la@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsJsonAsync($"/api/leagues/{leagueId}/seasons", new
        {
            name = "2025/26", startDate = "2025-09-01", endDate = "2026-04-30", weeks = Array.Empty<object>(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_EmptyWeeks_Allowed_Returns201()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsJsonAsync($"/api/leagues/{leagueId}/seasons", new
        {
            name = "2025/26", startDate = "2025-09-01", endDate = "2026-04-30", weeks = Array.Empty<object>(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_UnknownLeague_Returns404()
    {
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync($"/api/leagues/{Guid.NewGuid()}/seasons", new
        {
            name = "2025/26", startDate = "2025-09-01", endDate = "2026-04-30", weeks = Array.Empty<object>(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_AsNonAdmin_Returns403()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync($"/api/leagues/{leagueId}/seasons", new
        {
            name = "2025/26", startDate = "2025-09-01", endDate = "2026-04-30", weeks = Array.Empty<object>(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_EndBeforeStart_Returns400()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsJsonAsync($"/api/leagues/{leagueId}/seasons", new
        {
            name = "2025/26", startDate = "2026-04-30", endDate = "2025-09-01", weeks = Array.Empty<object>(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_WeekOutOfSeasonBounds_Returns400()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsJsonAsync($"/api/leagues/{leagueId}/seasons", new
        {
            name = "2025/26",
            startDate = "2025-09-01",
            endDate = "2025-09-30",
            weeks = new[] { Week("2025-10-01", "2025-10-07", "Level") },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_OverlappingWeeks_Returns400()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsJsonAsync($"/api/leagues/{leagueId}/seasons", new
        {
            name = "2025/26",
            startDate = "2025-09-01",
            endDate = "2025-12-31",
            weeks = new[]
            {
                Week("2025-09-01", "2025-09-10", "Level"),
                Week("2025-09-07", "2025-09-14", "Mixed"),
            },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_InvalidWeekType_Returns400()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsJsonAsync($"/api/leagues/{leagueId}/seasons", new
        {
            name = "2025/26",
            startDate = "2025-09-01",
            endDate = "2025-12-31",
            weeks = new[] { Week("2025-09-01", "2025-09-07", "Cup") },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_DuplicateNameInLeague_Returns409()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });
        await Client.PostAsJsonAsync($"/api/leagues/{leagueId}/seasons", new
        {
            name = "2025/26", startDate = "2025-09-01", endDate = "2026-04-30", weeks = Array.Empty<object>(),
        });

        var dup = await Client.PostAsJsonAsync($"/api/leagues/{leagueId}/seasons", new
        {
            name = "2025/26", startDate = "2025-09-01", endDate = "2026-04-30", weeks = Array.Empty<object>(),
        });

        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
