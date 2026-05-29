using System.Net;
using System.Net.Http.Json;
using smash_dates.Models;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class DeleteBlockedDateEndpointTests : IntegrationTestBase
{
    public DeleteBlockedDateEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Delete_AsClubAdmin_Returns204()
    {
        var admin = await Seeder.CreateUserAsync("admin@example.com", "correct-horse-battery");
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.GrantClubAdminAsync(clubId, admin.Id, admin.Id);
        var blockId = await Seeder.CreateBlockedDateAsync(clubId, BlockedDateScope.Club, new DateOnly(2025, 12, 25), new DateOnly(2025, 12, 25), "Holiday");
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "correct-horse-battery" });

        var response = await Client.DeleteAsync($"/api/clubs/{clubId}/blocked-dates/{blockId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_AsNonAdmin_Returns403()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        var blockId = await Seeder.CreateBlockedDateAsync(clubId, BlockedDateScope.Club, new DateOnly(2025, 12, 25), new DateOnly(2025, 12, 25), "Holiday");
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.DeleteAsync($"/api/clubs/{clubId}/blocked-dates/{blockId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_UnknownBlock_Returns404()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);

        var response = await Client.DeleteAsync($"/api/clubs/{clubId}/blocked-dates/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_BlockOfDifferentClub_Returns404()
    {
        var clubA = await Seeder.CreateClubAsync("Acme", "ACME");
        var clubB = await Seeder.CreateClubAsync("Beta", "BETA");
        var blockInB = await Seeder.CreateBlockedDateAsync(clubB, BlockedDateScope.Club, new DateOnly(2025, 12, 25), new DateOnly(2025, 12, 25), "Holiday");
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);

        var response = await Client.DeleteAsync($"/api/clubs/{clubA}/blocked-dates/{blockInB}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
