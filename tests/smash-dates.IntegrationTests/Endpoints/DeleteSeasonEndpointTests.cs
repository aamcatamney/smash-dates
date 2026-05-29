using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using smash_dates.Models;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class DeleteSeasonEndpointTests : IntegrationTestBase
{
    public DeleteSeasonEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Delete_DraftSeason_AsLeagueAdmin_Returns204()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var la = await Seeder.CreateUserAsync("la@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        await Seeder.GrantLeagueAdminAsync(leagueId, la.Id, sys.Id);
        var seasonId = await Seeder.CreateSeasonAsync(leagueId, "2025/26", new DateOnly(2025, 9, 1), new DateOnly(2026, 4, 30));
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "la@example.com", password = "correct-horse-battery" });

        var response = await Client.DeleteAsync($"/api/leagues/{leagueId}/seasons/{seasonId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_NonDraftSeason_Returns409()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        var seasonId = await Seeder.CreateSeasonAsync(
            leagueId, "2025/26", new DateOnly(2025, 9, 1), new DateOnly(2026, 4, 30), SeasonStatus.Active);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.DeleteAsync($"/api/leagues/{leagueId}/seasons/{seasonId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Delete_AsNonAdmin_Returns403()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        var seasonId = await Seeder.CreateSeasonAsync(leagueId, "2025/26", new DateOnly(2025, 9, 1), new DateOnly(2026, 4, 30));
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.DeleteAsync($"/api/leagues/{leagueId}/seasons/{seasonId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_UnknownSeason_Returns404()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.DeleteAsync($"/api/leagues/{leagueId}/seasons/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
