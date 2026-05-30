using System.Net;
using System.Net.Http.Json;
using smash_dates.Models;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class ListClubMatchesEndpointTests : IntegrationTestBase
{
    public ListClubMatchesEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record MatchDto(Guid Id, Guid HomeTeamId, string HomeTeamName, Guid AwayTeamId, string AwayTeamName, string Status);

    private sealed record Setup(Guid ClubA, Guid ClubC, Guid MatchId);

    private async Task<Setup> Arrange()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        var seasonId = await Seeder.CreateSeasonAsync(leagueId, "2025/26", new DateOnly(2025, 9, 1), new DateOnly(2025, 9, 30), SeasonStatus.Proposed);
        var divisionId = await Seeder.CreateDivisionAsync(leagueId, "Mens 1", DivisionGender.Mens, 1, 9);

        var clubA = await Seeder.CreateClubAsync("Acme", "ACME");
        var venueA = await Seeder.CreateVenueAsync(clubA, "Acme Hall", 1);
        var teamA = await Seeder.CreateTeamAsync(clubA, "Acme 1", DivisionGender.Mens);

        var clubB = await Seeder.CreateClubAsync("Beta", "BETA");
        var teamB = await Seeder.CreateTeamAsync(clubB, "Beta 1", DivisionGender.Mens);

        var clubC = await Seeder.CreateClubAsync("Gamma", "GAMM"); // uninvolved club

        var matchId = await Seeder.CreateMatchAsync(seasonId, divisionId, teamA, teamB, venueA, new DateOnly(2025, 9, 3));
        return new Setup(clubA, clubC, matchId);
    }

    [Fact]
    public async Task Get_ReturnsMatchesInvolvingClub_AnyAuthenticatedUser()
    {
        var s = await Arrange();
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var matches = await Client.GetFromJsonAsync<MatchDto[]>($"/api/clubs/{s.ClubA}/matches");

        matches.Should().NotBeNull();
        matches!.Length.Should().Be(1);
        matches[0].Id.Should().Be(s.MatchId);
        matches[0].HomeTeamName.Should().Be("Acme 1");
    }

    [Fact]
    public async Task Get_UninvolvedClub_ReturnsEmpty()
    {
        var s = await Arrange();
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var matches = await Client.GetFromJsonAsync<MatchDto[]>($"/api/clubs/{s.ClubC}/matches");

        matches!.Length.Should().Be(0);
    }

    [Fact]
    public async Task Get_Unauthenticated_Returns401()
    {
        var s = await Arrange();

        var response = await Client.GetAsync($"/api/clubs/{s.ClubA}/matches");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
