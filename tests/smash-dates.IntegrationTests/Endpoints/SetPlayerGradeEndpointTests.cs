using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class SetPlayerGradeEndpointTests : IntegrationTestBase
{
    public SetPlayerGradeEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record CreatedPlayerDto(Guid Id, string FullName, string Gender, int? Grade);

    private async Task<Guid> CreateLinkedPlayerAsync(Guid clubId)
    {
        var response = await Client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/players",
            new { fullName = "Test Player", gender = "Male", type = "Member" });
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<CreatedPlayerDto>();
        return dto!.Id;
    }

    [Fact]
    public async Task Patch_AsClubAdmin_SetsGrade_Returns204()
    {
        var admin = await Seeder.CreateUserAsync("admin-grade@example.com", "correct-horse-battery");
        var clubId = await Seeder.CreateClubAsync("GradeClub", "GRDC1");
        await Seeder.GrantClubAdminAsync(clubId, admin.Id, admin.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "admin-grade@example.com", password = "correct-horse-battery" });

        var playerId = await CreateLinkedPlayerAsync(clubId);

        var response = await Client.PatchAsJsonAsync(
            $"/api/clubs/{clubId}/players/{playerId}/grade",
            new { grade = 3 });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Patch_GradeSixOrHigher_Returns400()
    {
        var admin = await Seeder.CreateUserAsync("admin-grade2@example.com", "correct-horse-battery");
        var clubId = await Seeder.CreateClubAsync("GradeClub2", "GRDC2");
        await Seeder.GrantClubAdminAsync(clubId, admin.Id, admin.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "admin-grade2@example.com", password = "correct-horse-battery" });

        var playerId = await CreateLinkedPlayerAsync(clubId);

        var response = await Client.PatchAsJsonAsync(
            $"/api/clubs/{clubId}/players/{playerId}/grade",
            new { grade = 6 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
