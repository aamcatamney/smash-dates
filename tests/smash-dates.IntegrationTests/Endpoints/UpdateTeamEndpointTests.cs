using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using smash_dates.Models;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class UpdateTeamEndpointTests : IntegrationTestBase
{
    public UpdateTeamEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private static HttpRequestMessage Patch(string url, object body) =>
        new(HttpMethod.Patch, url) { Content = JsonContent.Create(body) };

    [Fact]
    public async Task Patch_AsClubAdmin_RenamesTeam_Returns204()
    {
        var admin = await Seeder.CreateUserAsync("admin@example.com", "correct-horse-battery");
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.GrantClubAdminAsync(clubId, admin.Id, admin.Id);
        var teamId = await Seeder.CreateTeamAsync(clubId, "Acme Mens 1", DivisionGender.Mens);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "correct-horse-battery" });

        var response = await Client.SendAsync(Patch($"/api/clubs/{clubId}/teams/{teamId}", new { name = "Acme Mens A" }));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Patch_AsNonAdmin_Returns403()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        var teamId = await Seeder.CreateTeamAsync(clubId, "Acme Mens 1", DivisionGender.Mens);
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.SendAsync(Patch($"/api/clubs/{clubId}/teams/{teamId}", new { name = "X" }));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Patch_UnknownTeam_Returns404()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);

        var response = await Client.SendAsync(Patch($"/api/clubs/{clubId}/teams/{Guid.NewGuid()}", new { name = "X" }));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Patch_TeamOfDifferentClub_Returns404()
    {
        var clubA = await Seeder.CreateClubAsync("Acme", "ACME");
        var clubB = await Seeder.CreateClubAsync("Beta", "BETA");
        var teamInB = await Seeder.CreateTeamAsync(clubB, "Beta Mens 1", DivisionGender.Mens);
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);

        var response = await Client.SendAsync(Patch($"/api/clubs/{clubA}/teams/{teamInB}", new { name = "X" }));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Patch_DuplicateName_Returns409()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.CreateTeamAsync(clubId, "Acme Mens 1", DivisionGender.Mens);
        var team2 = await Seeder.CreateTeamAsync(clubId, "Acme Mens 2", DivisionGender.Mens);
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);

        var response = await Client.SendAsync(Patch($"/api/clubs/{clubId}/teams/{team2}", new { name = "acme mens 1" }));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
