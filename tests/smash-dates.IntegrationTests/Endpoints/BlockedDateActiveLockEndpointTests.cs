using System.Net;
using System.Net.Http.Json;
using smash_dates.Models;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class BlockedDateActiveLockEndpointTests : IntegrationTestBase
{
    public BlockedDateActiveLockEndpointTests(PostgresFixture fixture) : base(fixture) { }

    // A club with a team entered in a season of the given status, plus a logged-in ClubAdmin.
    private async Task<Guid> ClubInSeason(SeasonStatus status)
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        var seasonId = await Seeder.CreateSeasonAsync(leagueId, "2025/26", new DateOnly(2025, 9, 1), new DateOnly(2026, 4, 30), status);
        var divisionId = await Seeder.CreateDivisionAsync(leagueId, "Mens 1", DivisionGender.Mens, 1, 9);
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        var clubAdmin = await Seeder.CreateUserAsync("ca@example.com", "correct-horse-battery");
        await Seeder.GrantClubAdminAsync(clubId, clubAdmin.Id, clubAdmin.Id);
        var teamId = await Seeder.CreateTeamAsync(clubId, "Acme 1", DivisionGender.Mens);
        await Seeder.CreateSeasonEntryAsync(seasonId, divisionId, teamId);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "ca@example.com", password = "correct-horse-battery" });
        return clubId;
    }

    [Fact]
    public async Task Create_WhenClubHasTeamInActiveSeason_Returns409()
    {
        var clubId = await ClubInSeason(SeasonStatus.Active);

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/blocked-dates", new
        {
            scope = "Club", startDate = "2025-12-25", endDate = "2025-12-25", reason = "Holiday",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_WhenNoActiveSeason_Allowed()
    {
        var clubId = await ClubInSeason(SeasonStatus.Proposed);

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/blocked-dates", new
        {
            scope = "Club", startDate = "2025-12-25", endDate = "2025-12-25", reason = "Holiday",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Delete_WhenClubHasTeamInActiveSeason_Returns409()
    {
        var clubId = await ClubInSeason(SeasonStatus.Active);
        var blockId = await Seeder.CreateBlockedDateAsync(clubId, BlockedDateScope.Club, new DateOnly(2025, 12, 25), new DateOnly(2025, 12, 25), "Holiday");

        var response = await Client.DeleteAsync($"/api/clubs/{clubId}/blocked-dates/{blockId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
