using System.Net;
using System.Net.Http.Json;
using smash_dates.Models;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class SeasonLifecycleEndpointTests : IntegrationTestBase
{
    public SeasonLifecycleEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record StatusResult(string Status);

    private async Task<(Guid leagueId, Guid seasonId)> Season(SeasonStatus status)
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        var seasonId = await Seeder.CreateSeasonAsync(leagueId, "2025/26", new DateOnly(2025, 9, 1), new DateOnly(2026, 4, 30), status);
        return (leagueId, seasonId);
    }

    private Task LoginSys() => Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

    [Fact]
    public async Task Activate_Proposed_BecomesActive()
    {
        var (leagueId, seasonId) = await Season(SeasonStatus.Proposed);
        await LoginSys();

        var response = await Client.PostAsync($"/api/leagues/{leagueId}/seasons/{seasonId}/activate", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<StatusResult>())!.Status.Should().Be("Active");
    }

    [Fact]
    public async Task Activate_NotProposed_Returns409()
    {
        var (leagueId, seasonId) = await Season(SeasonStatus.Draft);
        await LoginSys();

        var response = await Client.PostAsync($"/api/leagues/{leagueId}/seasons/{seasonId}/activate", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Activate_AsNonAdmin_Returns403()
    {
        var (leagueId, seasonId) = await Season(SeasonStatus.Proposed);
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsync($"/api/leagues/{leagueId}/seasons/{seasonId}/activate", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Activate_UnknownSeason_Returns404()
    {
        var (leagueId, _) = await Season(SeasonStatus.Proposed);
        await LoginSys();

        var response = await Client.PostAsync($"/api/leagues/{leagueId}/seasons/{Guid.NewGuid()}/activate", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Close_Active_BecomesClosed()
    {
        var (leagueId, seasonId) = await Season(SeasonStatus.Active);
        await LoginSys();

        var response = await Client.PostAsync($"/api/leagues/{leagueId}/seasons/{seasonId}/close", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<StatusResult>())!.Status.Should().Be("Closed");
    }

    [Fact]
    public async Task Close_NotActive_Returns409()
    {
        var (leagueId, seasonId) = await Season(SeasonStatus.Proposed);
        await LoginSys();

        var response = await Client.PostAsync($"/api/leagues/{leagueId}/seasons/{seasonId}/close", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
