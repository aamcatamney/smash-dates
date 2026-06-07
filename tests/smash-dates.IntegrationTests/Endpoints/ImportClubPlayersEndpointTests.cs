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
            csv = "name,gender,grade\nAlice Tan,Female,2\nBob Reyes,Male,",
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
    public async Task Import_NameExistingAtAnotherClub_CreatesDistinctPlayer()
    {
        // SystemAdmin so a single login can add players to both clubs.
        await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        var otherClub = await Seeder.CreateClubAsync("Beta", "BETA");
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });
        // An "Alice Tan" already exists at another club.
        await Client.PostAsJsonAsync($"/api/clubs/{otherClub}/players",
            new { fullName = "Alice Tan", gender = "Female", type = "Member" });

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/players/import", new
        {
            csv = "name,gender,grade\nAlice Tan,Female,",
        });

        var result = await response.Content.ReadFromJsonAsync<ImportResultDto>();
        result!.Created.Should().Be(1);
        result.Errors.Should().BeEmpty();

        // No reuse-by-name: the import creates this club's own Alice Tan, distinct from the other
        // club's. Duplicate identities are reconciled later by a separate merge.
        var otherRoster = await Client.GetFromJsonAsync<PlayerDto[]>($"/api/clubs/{otherClub}/players");
        var thisRoster = await Client.GetFromJsonAsync<PlayerDto[]>($"/api/clubs/{clubId}/players");
        thisRoster!.Single(p => p.FullName == "Alice Tan").PlayerId
            .Should().NotBe(otherRoster!.Single(p => p.FullName == "Alice Tan").PlayerId);
    }

    [Fact]
    public async Task Import_InvalidGenderAndGrade_ReportRowErrors_PartialImport()
    {
        var clubId = await ArrangeClubWithAdminAsync();

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/players/import", new
        {
            csv = "name,gender,grade\nValid Vic,Male,3\nBad Gender,Other,\nBad Grade,Female,9",
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
            csv = "name,gender,grade\nAlice,Female,",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
