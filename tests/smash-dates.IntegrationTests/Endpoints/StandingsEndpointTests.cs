using System.Net;
using System.Net.Http.Json;
using smash_dates.Models;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class StandingsEndpointTests : IntegrationTestBase
{
    public StandingsEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record Row(Guid TeamId, string TeamName, int Played, int Won, int Lost, int RubbersFor, int RubberDifference, int Points);
    private sealed record DivisionTable(Guid DivisionId, string DivisionName, Row[] Rows);

    [Fact]
    public async Task Get_ReturnsTableReflectingPlayedResults()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        var seasonId = await Seeder.CreateSeasonAsync(leagueId, "2025/26", new DateOnly(2025, 9, 1), new DateOnly(2025, 9, 30), SeasonStatus.Proposed);
        var divisionId = await Seeder.CreateDivisionAsync(leagueId, "Mens 1", DivisionGender.Mens, 1, 9);

        var clubA = await Seeder.CreateClubAsync("Acme", "ACME");
        var venueA = await Seeder.CreateVenueAsync(clubA, "Acme Hall", 1);
        var teamA = await Seeder.CreateTeamAsync(clubA, "Acme 1", DivisionGender.Mens);
        await Seeder.CreateSeasonEntryAsync(seasonId, divisionId, teamA);

        var clubB = await Seeder.CreateClubAsync("Beta", "BETA");
        var teamB = await Seeder.CreateTeamAsync(clubB, "Beta 1", DivisionGender.Mens);
        await Seeder.CreateSeasonEntryAsync(seasonId, divisionId, teamB);

        await Seeder.CreateMatchAsync(seasonId, divisionId, teamA, teamB, venueA, new DateOnly(2025, 9, 3),
            MatchStatus.Played, homeScore: 9, awayScore: 0);

        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var tables = await Client.GetFromJsonAsync<DivisionTable[]>($"/api/leagues/{leagueId}/seasons/{seasonId}/standings");

        tables.Should().NotBeNull();
        var table = tables!.Single(t => t.DivisionId == divisionId);
        table.Rows.Length.Should().Be(2);
        table.Rows[0].TeamId.Should().Be(teamA); // winner top
        table.Rows[0].Points.Should().Be(2);
        table.Rows[0].RubbersFor.Should().Be(9);
        table.Rows[0].Won.Should().Be(1);
        table.Rows[1].TeamId.Should().Be(teamB);
        table.Rows[1].Points.Should().Be(0);
    }

    [Fact]
    public async Task Get_Unauthenticated_Returns401()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        var seasonId = await Seeder.CreateSeasonAsync(leagueId, "2025/26", new DateOnly(2025, 9, 1), new DateOnly(2025, 9, 30), SeasonStatus.Proposed);

        var response = await Client.GetAsync($"/api/leagues/{leagueId}/seasons/{seasonId}/standings");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
