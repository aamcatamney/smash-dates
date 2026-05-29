using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using smash_dates.Models;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class ReplaceSeasonWeeksEndpointTests : IntegrationTestBase
{
    public ReplaceSeasonWeeksEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private static HttpRequestMessage Put(string url, object body) =>
        new(HttpMethod.Put, url) { Content = JsonContent.Create(body) };

    private static object Week(string start, string end, string type) =>
        new { startDate = start, endDate = end, weekType = type };

    [Fact]
    public async Task Put_AsLeagueAdmin_ReplacesWeeks_Returns204()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var la = await Seeder.CreateUserAsync("la@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        await Seeder.GrantLeagueAdminAsync(leagueId, la.Id, sys.Id);
        var seasonId = await Seeder.CreateSeasonAsync(leagueId, "2025/26", new DateOnly(2025, 9, 1), new DateOnly(2026, 4, 30));
        await Seeder.CreateSeasonWeekAsync(seasonId, new DateOnly(2025, 9, 1), new DateOnly(2025, 9, 7), WeekType.Level);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "la@example.com", password = "correct-horse-battery" });

        var response = await Client.SendAsync(Put($"/api/leagues/{leagueId}/seasons/{seasonId}/weeks", new
        {
            weeks = new[] { Week("2025-10-06", "2025-10-12", "Mixed"), Week("2025-10-13", "2025-10-19", "Level") },
        }));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Put_OverlappingWeeks_Returns400()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        var seasonId = await Seeder.CreateSeasonAsync(leagueId, "2025/26", new DateOnly(2025, 9, 1), new DateOnly(2026, 4, 30));
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.SendAsync(Put($"/api/leagues/{leagueId}/seasons/{seasonId}/weeks", new
        {
            weeks = new[] { Week("2025-09-01", "2025-09-10", "Level"), Week("2025-09-05", "2025-09-12", "Mixed") },
        }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_NotDraft_Returns409()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        var seasonId = await Seeder.CreateSeasonAsync(
            leagueId, "2025/26", new DateOnly(2025, 9, 1), new DateOnly(2026, 4, 30), SeasonStatus.Active);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.SendAsync(Put($"/api/leagues/{leagueId}/seasons/{seasonId}/weeks", new
        {
            weeks = new[] { Week("2025-10-06", "2025-10-12", "Mixed") },
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Put_AsNonAdmin_Returns403()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        var seasonId = await Seeder.CreateSeasonAsync(leagueId, "2025/26", new DateOnly(2025, 9, 1), new DateOnly(2026, 4, 30));
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.SendAsync(Put($"/api/leagues/{leagueId}/seasons/{seasonId}/weeks", new
        {
            weeks = new[] { Week("2025-10-06", "2025-10-12", "Mixed") },
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Put_UnknownSeason_Returns404()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.SendAsync(Put($"/api/leagues/{leagueId}/seasons/{Guid.NewGuid()}/weeks", new
        {
            weeks = Array.Empty<object>(),
        }));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
