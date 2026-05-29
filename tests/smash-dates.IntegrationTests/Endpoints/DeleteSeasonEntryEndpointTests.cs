using System.Net;
using System.Net.Http.Json;
using smash_dates.Models;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class DeleteSeasonEntryEndpointTests : IntegrationTestBase
{
    public DeleteSeasonEntryEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private async Task<(Guid leagueId, Guid seasonId, Guid entryId)> ArrangeEntry(SeasonStatus status = SeasonStatus.Draft)
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        var seasonId = await Seeder.CreateSeasonAsync(leagueId, "2025/26", new DateOnly(2025, 9, 1), new DateOnly(2026, 4, 30), status);
        var divisionId = await Seeder.CreateDivisionAsync(leagueId, "Mens 1", DivisionGender.Mens, 1, 9);
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        var teamId = await Seeder.CreateTeamAsync(clubId, "Acme 1", DivisionGender.Mens);
        var entryId = await Seeder.CreateSeasonEntryAsync(seasonId, divisionId, teamId);
        return (leagueId, seasonId, entryId);
    }

    [Fact]
    public async Task Delete_AsLeagueAdmin_Draft_Returns204()
    {
        var (leagueId, seasonId, entryId) = await ArrangeEntry();
        var la = await Seeder.CreateUserAsync("la@example.com", "correct-horse-battery");
        await Seeder.GrantLeagueAdminAsync(leagueId, la.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "la@example.com", password = "correct-horse-battery" });

        var response = await Client.DeleteAsync($"/api/leagues/{leagueId}/seasons/{seasonId}/entries/{entryId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_AsNonAdmin_Returns403()
    {
        var (leagueId, seasonId, entryId) = await ArrangeEntry();
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.DeleteAsync($"/api/leagues/{leagueId}/seasons/{seasonId}/entries/{entryId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_UnknownEntry_Returns404()
    {
        var (leagueId, seasonId, _) = await ArrangeEntry();
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.DeleteAsync($"/api/leagues/{leagueId}/seasons/{seasonId}/entries/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_EntryOfDifferentSeason_Returns404()
    {
        var (leagueId, _, entryId) = await ArrangeEntry();
        var otherSeason = await Seeder.CreateSeasonAsync(leagueId, "2024/25", new DateOnly(2024, 9, 1), new DateOnly(2025, 4, 30));
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.DeleteAsync($"/api/leagues/{leagueId}/seasons/{otherSeason}/entries/{entryId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_SeasonNotDraft_Returns409()
    {
        var (leagueId, seasonId, entryId) = await ArrangeEntry(SeasonStatus.Active);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.DeleteAsync($"/api/leagues/{leagueId}/seasons/{seasonId}/entries/{entryId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
