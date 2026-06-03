using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class ImportClubPlayersEndpointTests : IntegrationTestBase
{
    public ImportClubPlayersEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record RowErrorDto(int Row, string Message);
    private sealed record ImportResultDto(int Created, int Updated, RowErrorDto[] Errors);
    private sealed record PlayerDto(Guid PlayerId, string FullName, string Gender, string Type, int? Grade);

    private async Task<Guid> ArrangeClubWithAdminAsync()
    {
        var admin = await Seeder.CreateUserAsync("admin@example.com", "correct-horse-battery");
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.GrantClubAdminAsync(clubId, admin.Id, admin.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "correct-horse-battery" });
        return clubId;
    }

    [Fact]
    public async Task Import_NewPlayers_CreatesAsMembers_WithGrade()
    {
        var clubId = await ArrangeClubWithAdminAsync();

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/players/import", new
        {
            csv = "name,gender,grade,useExisting\nAlice Tan,Female,2,\nBob Reyes,Male,,",
        });

        var result = await response.Content.ReadFromJsonAsync<ImportResultDto>();
        result!.Created.Should().Be(2);
        result.Errors.Should().BeEmpty();

        var players = await Client.GetFromJsonAsync<PlayerDto[]>($"/api/clubs/{clubId}/players");
        players!.Should().HaveCount(2);
        players.Should().OnlyContain(p => p.Type == "Member");
        players.Single(p => p.FullName == "Alice Tan").Grade.Should().Be(2);
        players.Single(p => p.FullName == "Bob Reyes").Grade.Should().BeNull();
    }

    [Fact]
    public async Task Import_UseExisting_LinksTheExistingGlobalPlayer_NoDuplicate()
    {
        var clubId = await ArrangeClubWithAdminAsync();
        var otherClub = await Seeder.CreateClubAsync("Beta", "BETA");
        // Alice already exists globally (affiliated to another club).
        await Client.PostAsJsonAsync($"/api/clubs/{otherClub}/players",
            new { fullName = "Alice Tan", gender = "Female", type = "Member" });

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/players/import", new
        {
            csv = "name,gender,grade,useExisting\nAlice Tan,Female,,true",
        });

        var result = await response.Content.ReadFromJsonAsync<ImportResultDto>();
        result!.Created.Should().Be(1);
        result.Errors.Should().BeEmpty();

        // Cross-club search finds exactly one Alice Tan — the import reused the global player.
        var found = await Client.GetFromJsonAsync<JsonPlayer[]>("/api/players?search=Alice");
        found!.Count(p => p.FullName == "Alice Tan").Should().Be(1);
    }

    [Fact]
    public async Task Import_UseExisting_AmbiguousMatch_ReportsRowError()
    {
        var clubId = await ArrangeClubWithAdminAsync();
        // Two global "Sam Lee (Male)" players via two non-useExisting imports.
        await Client.PostAsJsonAsync($"/api/clubs/{clubId}/players/import", new
        {
            csv = "name,gender,grade,useExisting\nSam Lee,Male,,\nSam Lee,Male,,",
        });

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/players/import", new
        {
            csv = "name,gender,grade,useExisting\nSam Lee,Male,,true",
        });

        var result = await response.Content.ReadFromJsonAsync<ImportResultDto>();
        result!.Created.Should().Be(0);
        result.Errors.Should().ContainSingle(e => e.Row == 2);
    }

    [Fact]
    public async Task Import_InvalidGenderAndGrade_ReportRowErrors_PartialImport()
    {
        var clubId = await ArrangeClubWithAdminAsync();

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/players/import", new
        {
            csv = "name,gender,grade,useExisting\nValid Vic,Male,3,\nBad Gender,Other,,\nBad Grade,Female,9,",
        });

        var result = await response.Content.ReadFromJsonAsync<ImportResultDto>();
        result!.Created.Should().Be(1);
        result.Errors.Should().HaveCount(2);
    }

    [Fact]
    public async Task Import_AsNonAdmin_Returns403()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/players/import", new
        {
            csv = "name,gender,grade,useExisting\nAlice,Female,,",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private sealed record JsonPlayer(Guid Id, string FullName, string Gender, int? Grade);
}
