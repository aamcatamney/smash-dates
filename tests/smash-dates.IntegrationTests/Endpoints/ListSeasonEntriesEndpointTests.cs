using System.Net;
using System.Net.Http.Json;
using smash_dates.Models;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class ListSeasonEntriesEndpointTests : IntegrationTestBase
{
    public ListSeasonEntriesEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record EntryDto(
        Guid Id, Guid SeasonId, Guid DivisionId, string DivisionName, Guid TeamId, string TeamName, string Gender);

    [Fact]
    public async Task Get_ReturnsEntriesWithNames_AnyAuthenticatedUser()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        var seasonId = await Seeder.CreateSeasonAsync(leagueId, "2025/26", new DateOnly(2025, 9, 1), new DateOnly(2026, 4, 30));
        var divisionId = await Seeder.CreateDivisionAsync(leagueId, "Mens 1", DivisionGender.Mens, 1, 9);
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        var teamId = await Seeder.CreateTeamAsync(clubId, "Acme 1", DivisionGender.Mens);
        await Seeder.CreateSeasonEntryAsync(seasonId, divisionId, teamId);
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var entries = await Client.GetFromJsonAsync<EntryDto[]>($"/api/leagues/{leagueId}/seasons/{seasonId}/entries");

        entries.Should().NotBeNull();
        entries!.Length.Should().Be(1);
        entries[0].TeamName.Should().Be("Acme 1");
        entries[0].DivisionName.Should().Be("Mens 1");
    }

    [Fact]
    public async Task Get_Unauthenticated_Returns401()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        var seasonId = await Seeder.CreateSeasonAsync(leagueId, "2025/26", new DateOnly(2025, 9, 1), new DateOnly(2026, 4, 30));

        var response = await Client.GetAsync($"/api/leagues/{leagueId}/seasons/{seasonId}/entries");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
