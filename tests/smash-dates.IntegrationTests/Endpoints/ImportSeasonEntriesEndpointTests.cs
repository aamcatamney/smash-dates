using System.Net;
using System.Net.Http.Json;
using smash_dates.Models;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class ImportSeasonEntriesEndpointTests : IntegrationTestBase
{
    public ImportSeasonEntriesEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record RowErrorDto(int Row, string Message);
    private sealed record ImportResultDto(int Created, int Updated, RowErrorDto[] Errors);
    private sealed record EntryDto(Guid Id, string TeamName, string DivisionName);

    private sealed record Setup(Guid LeagueId, Guid SeasonId, Guid Div1, Guid Div2, Guid TeamId);

    private async Task<Setup> ArrangeAsync(
        DivisionGender div1Gender = DivisionGender.Mens,
        DivisionGender teamGender = DivisionGender.Mens,
        SeasonStatus seasonStatus = SeasonStatus.Draft)
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        var seasonId = await Seeder.CreateSeasonAsync(leagueId, "2025/26", new DateOnly(2025, 9, 1), new DateOnly(2026, 4, 30), seasonStatus);
        var div1 = await Seeder.CreateDivisionAsync(leagueId, "Mens Division 1", div1Gender, 1, 9);
        var div2 = await Seeder.CreateDivisionAsync(leagueId, "Mens Division 2", DivisionGender.Mens, 2, 9);
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.CreateMembershipAsync(clubId, leagueId, MembershipStatus.Accepted);
        var teamId = await Seeder.CreateTeamAsync(clubId, "Acme 1st", teamGender);
        return new Setup(leagueId, seasonId, div1, div2, teamId);
    }

    private Task LoginSystemAdmin() =>
        Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

    [Fact]
    public async Task Import_ResolvesNames_CreatesEntry()
    {
        var s = await ArrangeAsync();
        await LoginSystemAdmin();

        var response = await Client.PostAsJsonAsync(
            $"/api/leagues/{s.LeagueId}/seasons/{s.SeasonId}/entries/import",
            new { csv = "team,division\nAcme 1st,Mens Division 1" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ImportResultDto>();
        result!.Created.Should().Be(1);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Import_TeamAlreadyEntered_MovesDivision()
    {
        var s = await ArrangeAsync();
        await Seeder.CreateSeasonEntryAsync(s.SeasonId, s.Div1, s.TeamId);
        await LoginSystemAdmin();

        var response = await Client.PostAsJsonAsync(
            $"/api/leagues/{s.LeagueId}/seasons/{s.SeasonId}/entries/import",
            new { csv = "team,division\nAcme 1st,Mens Division 2" });

        var result = await response.Content.ReadFromJsonAsync<ImportResultDto>();
        result!.Updated.Should().Be(1);
        result.Created.Should().Be(0);

        var entries = await Client.GetFromJsonAsync<EntryDto[]>($"/api/leagues/{s.LeagueId}/seasons/{s.SeasonId}/entries");
        entries!.Single(e => e.TeamName == "Acme 1st").DivisionName.Should().Be("Mens Division 2");
    }

    [Fact]
    public async Task Import_GenderMismatch_ReportsRowError()
    {
        var s = await ArrangeAsync(div1Gender: DivisionGender.Ladies, teamGender: DivisionGender.Mens);
        await LoginSystemAdmin();

        var response = await Client.PostAsJsonAsync(
            $"/api/leagues/{s.LeagueId}/seasons/{s.SeasonId}/entries/import",
            new { csv = "team,division\nAcme 1st,Mens Division 1" });

        var result = await response.Content.ReadFromJsonAsync<ImportResultDto>();
        result!.Created.Should().Be(0);
        result.Errors.Should().ContainSingle(e => e.Message.Contains("gender"));
    }

    [Fact]
    public async Task Import_UnknownTeamOrDivision_ReportsRowErrors()
    {
        var s = await ArrangeAsync();
        await LoginSystemAdmin();

        var response = await Client.PostAsJsonAsync(
            $"/api/leagues/{s.LeagueId}/seasons/{s.SeasonId}/entries/import",
            new { csv = "team,division\nGhost,Mens Division 1\nAcme 1st,No Such Division" });

        var result = await response.Content.ReadFromJsonAsync<ImportResultDto>();
        result!.Created.Should().Be(0);
        result.Errors.Should().HaveCount(2);
    }

    [Fact]
    public async Task Import_NonDraftSeason_Returns409()
    {
        var s = await ArrangeAsync(seasonStatus: SeasonStatus.Active);
        await LoginSystemAdmin();

        var response = await Client.PostAsJsonAsync(
            $"/api/leagues/{s.LeagueId}/seasons/{s.SeasonId}/entries/import",
            new { csv = "team,division\nAcme 1st,Mens Division 1" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
