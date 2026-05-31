using System.Net;
using System.Net.Http.Json;
using smash_dates.Models;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class TeamSquadEndpointsTests : IntegrationTestBase
{
    public TeamSquadEndpointsTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record PlayerDto(Guid Id, string FullName, string Gender);
    private sealed record CreatedDto(Guid Id);
    private sealed record SquadDto(Guid PlayerId, string FullName, string Gender);

    private sealed record Setup(Guid LeagueId, Guid ClubId, Guid MensTeamId);

    // League L1 with a Mens division, a Draft season, a club (accepted member) whose Mens team
    // is entered in that division — so the team is "entered in L1". SystemAdmin logged in.
    private async Task<Setup> ArrangeAsync()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("L1", admin.Id);
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME", contactEmail: "acme@club.test");
        await Seeder.CreateMembershipAsync(clubId, leagueId, MembershipStatus.Accepted);
        var divisionId = await Seeder.CreateDivisionAsync(leagueId, "Mens 1", DivisionGender.Mens, 1, 9);
        var seasonId = await Seeder.CreateSeasonAsync(leagueId, "2025/26", new DateOnly(2025, 9, 1), new DateOnly(2026, 4, 30), SeasonStatus.Draft);
        var teamId = await Seeder.CreateTeamAsync(clubId, "Acme 1st", DivisionGender.Mens);
        await Seeder.CreateSeasonEntryAsync(seasonId, divisionId, teamId);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });
        return new Setup(leagueId, clubId, teamId);
    }

    private async Task<Guid> AddPlayerAsync(Guid clubId, string name, string gender)
    {
        var p = await (await Client.PostAsJsonAsync($"/api/clubs/{clubId}/players",
            new { fullName = name, gender, type = "Member" })).Content.ReadFromJsonAsync<PlayerDto>();
        return p!.Id;
    }

    private async Task ConfirmLevelAsync(Guid clubId, Guid playerId, Guid leagueId)
    {
        var reg = await (await Client.PostAsJsonAsync($"/api/clubs/{clubId}/players/{playerId}/registrations",
            new { leagueId, discipline = "Level" })).Content.ReadFromJsonAsync<CreatedDto>();
        await Client.PostAsync($"/api/leagues/{leagueId}/registrations/{reg!.Id}/confirm", null);
    }

    [Fact]
    public async Task Add_EligiblePlayer_Succeeds()
    {
        var s = await ArrangeAsync();
        var playerId = await AddPlayerAsync(s.ClubId, "Sam Okafor", "Male");
        await ConfirmLevelAsync(s.ClubId, playerId, s.LeagueId);

        var response = await Client.PostAsJsonAsync($"/api/clubs/{s.ClubId}/teams/{s.MensTeamId}/players", new { playerId });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var squad = await Client.GetFromJsonAsync<SquadDto[]>($"/api/clubs/{s.ClubId}/teams/{s.MensTeamId}/players");
        squad!.Should().ContainSingle(m => m.PlayerId == playerId);
    }

    [Fact]
    public async Task Add_GenderMismatch_Returns409()
    {
        var s = await ArrangeAsync();
        var playerId = await AddPlayerAsync(s.ClubId, "Jane Patel", "Female");
        await ConfirmLevelAsync(s.ClubId, playerId, s.LeagueId);

        var response = await Client.PostAsJsonAsync($"/api/clubs/{s.ClubId}/teams/{s.MensTeamId}/players", new { playerId });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Add_NotRegistered_Returns409()
    {
        var s = await ArrangeAsync();
        var playerId = await AddPlayerAsync(s.ClubId, "Sam Okafor", "Male"); // no confirmed registration

        var response = await Client.PostAsJsonAsync($"/api/clubs/{s.ClubId}/teams/{s.MensTeamId}/players", new { playerId });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Add_RegisteredInDifferentLeague_Returns409()
    {
        var s = await ArrangeAsync();
        // A second league the club belongs to, but the team is NOT entered in.
        var league2 = await Seeder.CreateLeagueAsync("L2", (await Seeder.CreateUserAsync("x@example.com", "correct-horse-battery")).Id);
        await Seeder.CreateMembershipAsync(s.ClubId, league2, MembershipStatus.Accepted);
        var playerId = await AddPlayerAsync(s.ClubId, "Sam Okafor", "Male");
        await ConfirmLevelAsync(s.ClubId, playerId, league2); // confirmed in L2, team entered only in L1

        var response = await Client.PostAsJsonAsync($"/api/clubs/{s.ClubId}/teams/{s.MensTeamId}/players", new { playerId });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Remove_DropsFromSquad()
    {
        var s = await ArrangeAsync();
        var playerId = await AddPlayerAsync(s.ClubId, "Sam Okafor", "Male");
        await ConfirmLevelAsync(s.ClubId, playerId, s.LeagueId);
        await Client.PostAsJsonAsync($"/api/clubs/{s.ClubId}/teams/{s.MensTeamId}/players", new { playerId });

        var response = await Client.DeleteAsync($"/api/clubs/{s.ClubId}/teams/{s.MensTeamId}/players/{playerId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var squad = await Client.GetFromJsonAsync<SquadDto[]>($"/api/clubs/{s.ClubId}/teams/{s.MensTeamId}/players");
        squad!.Should().BeEmpty();
    }
}
