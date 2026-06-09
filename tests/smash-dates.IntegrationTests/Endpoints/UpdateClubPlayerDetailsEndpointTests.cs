using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

// Editing a roster player's name + grade (PATCH /players/{id}/details). Gender is immutable.
public sealed class UpdateClubPlayerDetailsEndpointTests : IntegrationTestBase
{
    public UpdateClubPlayerDetailsEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record PlayerDto(Guid Id, string FullName, string Gender, int? Grade);
    private sealed record RosterRow(Guid PlayerId, string FullName, string Gender, string Type, int? Grade);

    private async Task<(Guid ClubId, Guid PlayerId)> ArrangeAsync()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);
        var created = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/players",
            new { fullName = "Alise Tan", gender = "Female", type = "Member" });
        var player = (await created.Content.ReadFromJsonAsync<PlayerDto>())!;
        return (clubId, player.Id);
    }

    [Fact]
    public async Task Update_RenamesAndSetsGrade_GenderUnchanged()
    {
        var (clubId, playerId) = await ArrangeAsync();

        var response = await Client.PatchAsJsonAsync($"/api/clubs/{clubId}/players/{playerId}/details",
            new { fullName = "Alice Tan", grade = 2 });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var roster = await Client.GetFromJsonAsync<List<RosterRow>>($"/api/clubs/{clubId}/players");
        var row = roster!.Single(p => p.PlayerId == playerId);
        row.FullName.Should().Be("Alice Tan");
        row.Grade.Should().Be(2);
        row.Gender.Should().Be("Female");
    }

    [Fact]
    public async Task Update_NullGrade_ClearsIt()
    {
        var (clubId, playerId) = await ArrangeAsync();
        await Client.PatchAsJsonAsync($"/api/clubs/{clubId}/players/{playerId}/details",
            new { fullName = "Alise Tan", grade = 3 });

        var response = await Client.PatchAsJsonAsync($"/api/clubs/{clubId}/players/{playerId}/details",
            new { fullName = "Alise Tan", grade = (int?)null });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var roster = await Client.GetFromJsonAsync<List<RosterRow>>($"/api/clubs/{clubId}/players");
        roster!.Single(p => p.PlayerId == playerId).Grade.Should().BeNull();
    }

    [Fact]
    public async Task Update_PlayerNotOnThisClub_Returns404()
    {
        var (_, playerId) = await ArrangeAsync();
        var otherClub = await Seeder.CreateClubAsync("Other", "OTHR");

        var response = await Client.PatchAsJsonAsync($"/api/clubs/{otherClub}/players/{playerId}/details",
            new { fullName = "Hijack", grade = (int?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_AsNonAdmin_Returns403()
    {
        var (clubId, playerId) = await ArrangeAsync();
        using var other = Factory.CreateClient();
        await other.LoginAsAsync("pleb@example.com", "correct-horse-battery", Seeder);

        var response = await other.PatchAsJsonAsync($"/api/clubs/{clubId}/players/{playerId}/details",
            new { fullName = "Nope", grade = (int?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_InvalidGrade_Returns400()
    {
        var (clubId, playerId) = await ArrangeAsync();

        var response = await Client.PatchAsJsonAsync($"/api/clubs/{clubId}/players/{playerId}/details",
            new { fullName = "Alise Tan", grade = 9 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
