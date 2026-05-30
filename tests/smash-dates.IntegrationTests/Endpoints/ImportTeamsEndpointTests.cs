using System.Net;
using System.Net.Http.Json;
using smash_dates.Models;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class ImportTeamsEndpointTests : IntegrationTestBase
{
    public ImportTeamsEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record RowErrorDto(int Row, string Message);
    private sealed record ImportResultDto(int Created, int Updated, RowErrorDto[] Errors);
    private sealed record TeamDto(Guid Id, string Name, string Gender);

    private async Task<Guid> ArrangeClubWithAdminAsync()
    {
        var admin = await Seeder.CreateUserAsync("admin@example.com", "correct-horse-battery");
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.GrantClubAdminAsync(clubId, admin.Id, admin.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "correct-horse-battery" });
        return clubId;
    }

    [Fact]
    public async Task Import_NewTeams_CreatesAllAndReportsCount()
    {
        var clubId = await ArrangeClubWithAdminAsync();

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/teams/import", new
        {
            csv = "name,gender\nAcme 1st,Mens\nAcme Ladies,Ladies",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ImportResultDto>();
        result!.Created.Should().Be(2);
        result.Updated.Should().Be(0);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Import_PartialErrors_ImportsGoodRowsAndReportsBad()
    {
        var clubId = await ArrangeClubWithAdminAsync();

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/teams/import", new
        {
            csv = "name,gender\nAcme 1st,Mens\nBad Team,Vegan\n,Mens",
        });

        var result = await response.Content.ReadFromJsonAsync<ImportResultDto>();
        result!.Created.Should().Be(1);
        result.Errors.Should().HaveCount(2);
        result.Errors.Should().Contain(e => e.Row == 3); // invalid gender on line 3
        result.Errors.Should().Contain(e => e.Row == 4); // empty name on line 4
    }

    [Fact]
    public async Task Import_ExistingTeam_SameGenderCountedUpdated_DifferentGenderErrors()
    {
        var admin = await Seeder.CreateUserAsync("admin@example.com", "correct-horse-battery");
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.GrantClubAdminAsync(clubId, admin.Id, admin.Id);
        await Seeder.CreateTeamAsync(clubId, "Acme 1st", DivisionGender.Mens);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/teams/import", new
        {
            csv = "name,gender\nAcme 1st,Mens\nAcme 1st,Ladies",
        });

        var result = await response.Content.ReadFromJsonAsync<ImportResultDto>();
        result!.Updated.Should().Be(1);   // same gender — unchanged
        result.Created.Should().Be(0);
        result.Errors.Should().ContainSingle(e => e.Message.Contains("immutable"));
    }

    [Fact]
    public async Task Import_MissingColumn_Returns400()
    {
        var clubId = await ArrangeClubWithAdminAsync();

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/teams/import", new
        {
            csv = "name\nAcme 1st",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Import_AsNonAdmin_Returns403()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/teams/import", new
        {
            csv = "name,gender\nAcme 1st,Mens",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Import_PersistsTeams()
    {
        var clubId = await ArrangeClubWithAdminAsync();
        await Client.PostAsJsonAsync($"/api/clubs/{clubId}/teams/import", new
        {
            csv = "name,gender\nAcme 1st,Mens\nAcme Ladies,Ladies",
        });

        var teams = await Client.GetFromJsonAsync<TeamDto[]>($"/api/clubs/{clubId}/teams");
        teams!.Select(t => t.Name).Should().BeEquivalentTo("Acme 1st", "Acme Ladies");
    }
}
