using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class PlayerEndpointsTests : IntegrationTestBase
{
    public PlayerEndpointsTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record PlayerDto(Guid Id, string FullName, string Gender);
    private sealed record PlayerLinkDto(Guid PlayerId, string FullName, string Gender, string Type);

    [Fact]
    public async Task Add_CreatesGlobalPlayerAndLinksAsMember()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/players",
            new { fullName = "Jane Smith", gender = "Female", type = "Member" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var player = await response.Content.ReadFromJsonAsync<PlayerDto>();
        player!.FullName.Should().Be("Jane Smith");

        var links = await Client.GetFromJsonAsync<PlayerLinkDto[]>($"/api/clubs/{clubId}/players");
        links!.Should().ContainSingle(l => l.PlayerId == player.Id && l.Type == "Member" && l.Gender == "Female");
    }

    [Fact]
    public async Task Add_ExistingGlobalPlayer_LinksToSecondClubAsVisitor()
    {
        var clubA = await Seeder.CreateClubAsync("Acme", "ACME");
        var clubB = await Seeder.CreateClubAsync("Beta", "BETA");
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);
        var created = await (await Client.PostAsJsonAsync($"/api/clubs/{clubA}/players",
            new { fullName = "Jane Smith", gender = "Female", type = "Member" })).Content.ReadFromJsonAsync<PlayerDto>();

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubB}/players",
            new { playerId = created!.Id, type = "Visitor" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var bLinks = await Client.GetFromJsonAsync<PlayerLinkDto[]>($"/api/clubs/{clubB}/players");
        bLinks!.Should().ContainSingle(l => l.PlayerId == created.Id && l.Type == "Visitor");
    }

    [Fact]
    public async Task UpdateLink_ChangesType()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);
        var player = await (await Client.PostAsJsonAsync($"/api/clubs/{clubId}/players",
            new { fullName = "Jane Smith", gender = "Female", type = "Visitor" })).Content.ReadFromJsonAsync<PlayerDto>();

        var response = await Client.PatchAsJsonAsync($"/api/clubs/{clubId}/players/{player!.Id}", new { type = "Member" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var links = await Client.GetFromJsonAsync<PlayerLinkDto[]>($"/api/clubs/{clubId}/players");
        links!.Single().Type.Should().Be("Member");
    }

    [Fact]
    public async Task Add_AsNonAdmin_Returns403()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/players",
            new { fullName = "Jane Smith", gender = "Female", type = "Member" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Search_FindsByName()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);
        await Client.PostAsJsonAsync($"/api/clubs/{clubId}/players", new { fullName = "Jane Smith", gender = "Female", type = "Member" });

        var found = await Client.GetFromJsonAsync<PlayerDto[]>("/api/players?search=jane");
        found!.Should().ContainSingle(p => p.FullName == "Jane Smith");
    }
}
