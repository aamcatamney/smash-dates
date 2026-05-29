using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using smash_dates.Models;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class DeleteTeamEndpointTests : IntegrationTestBase
{
    public DeleteTeamEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Delete_AsClubAdmin_Returns204()
    {
        var admin = await Seeder.CreateUserAsync("admin@example.com", "correct-horse-battery");
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.GrantClubAdminAsync(clubId, admin.Id, admin.Id);
        var teamId = await Seeder.CreateTeamAsync(clubId, "Acme Mens 1", DivisionGender.Mens);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "correct-horse-battery" });

        var response = await Client.DeleteAsync($"/api/clubs/{clubId}/teams/{teamId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_AsNonAdmin_Returns403()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        var teamId = await Seeder.CreateTeamAsync(clubId, "Acme Mens 1", DivisionGender.Mens);
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.DeleteAsync($"/api/clubs/{clubId}/teams/{teamId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_UnknownTeam_Returns404()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);

        var response = await Client.DeleteAsync($"/api/clubs/{clubId}/teams/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_TeamWithSeasonEntry_Returns409()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        var seasonId = await Seeder.CreateSeasonAsync(leagueId, "2025/26", new DateOnly(2025, 9, 1), new DateOnly(2026, 4, 30));
        var divisionId = await Seeder.CreateDivisionAsync(leagueId, "Mens 1", DivisionGender.Mens, 1, 9);
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        var teamId = await Seeder.CreateTeamAsync(clubId, "Acme 1", DivisionGender.Mens);
        await Seeder.CreateSeasonEntryAsync(seasonId, divisionId, teamId);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.DeleteAsync($"/api/clubs/{clubId}/teams/{teamId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
