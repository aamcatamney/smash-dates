using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class CreateTeamEndpointTests : IntegrationTestBase
{
    public CreateTeamEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Post_AsClubAdmin_CreatesTeam_Returns201()
    {
        var admin = await Seeder.CreateUserAsync("admin@example.com", "correct-horse-battery");
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.GrantClubAdminAsync(clubId, admin.Id, admin.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/teams", new
        {
            name = "Acme Mens 1",
            gender = "Mens",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_AsSystemAdmin_CreatesTeam_Returns201()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/teams", new
        {
            name = "Acme Ladies 1",
            gender = "Ladies",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_UnknownClub_Returns404()
    {
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync($"/api/clubs/{Guid.NewGuid()}/teams", new
        {
            name = "Ghost Mens 1", gender = "Mens",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_InvalidGender_Returns400()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/teams", new
        {
            name = "Acme Mens 1", gender = "Vegan",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_DuplicateNameInClub_Returns409()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);
        await Client.PostAsJsonAsync($"/api/clubs/{clubId}/teams", new { name = "Acme Mens 1", gender = "Mens" });

        var dup = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/teams", new { name = "acme mens 1", gender = "Mens" });

        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Post_AsNonAdmin_Returns403()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/teams", new
        {
            name = "Acme Mens 1", gender = "Mens",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
