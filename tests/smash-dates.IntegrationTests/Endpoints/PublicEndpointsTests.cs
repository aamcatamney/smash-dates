using System.Net;
using System.Net.Http.Json;
using smash_dates.Models;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

// The public view is anonymous (no login) and read-only. These tests never call LoginAs.
public sealed class PublicEndpointsTests : IntegrationTestBase
{
    public PublicEndpointsTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record LeagueDto(Guid Id, string Name, string? Description);
    private sealed record SeasonDto(Guid Id, string Name, string Status);
    private sealed record LeagueDetailDto(Guid Id, string Name, string? Description, SeasonDto[] Seasons);
    private sealed record RowDto(Guid TeamId, string TeamName, int Played, int Won, int Points, int RubbersFor);
    private sealed record TableDto(Guid DivisionId, string DivisionName, RowDto[] Rows);
    private sealed record FixtureDto(
        Guid Id, string DivisionName, string HomeTeamName, string AwayTeamName, string VenueName,
        string Status, int? HomeScore, int? AwayScore, bool IsWalkover);

    private async Task<(Guid League, Guid Season, Guid Division, Guid TeamA, Guid TeamB)> SeedPlayedSeason()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("North League", admin.Id);
        var seasonId = await Seeder.CreateSeasonAsync(leagueId, "2025/26", new DateOnly(2025, 9, 1), new DateOnly(2025, 9, 30), SeasonStatus.Active);
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

        return (leagueId, seasonId, divisionId, teamA, teamB);
    }

    [Fact]
    public async Task Leagues_AreListedAnonymously()
    {
        var (leagueId, _, _, _, _) = await SeedPlayedSeason();

        var leagues = await Client.GetFromJsonAsync<LeagueDto[]>("/api/public/leagues");

        leagues!.Should().Contain(l => l.Id == leagueId && l.Name == "North League");
    }

    [Fact]
    public async Task League_ListsOnlyScheduledSeasons()
    {
        var (leagueId, seasonId, _, _, _) = await SeedPlayedSeason();
        // A Draft season has no public schedule and should not appear.
        await Seeder.CreateSeasonAsync(leagueId, "Draft season", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), SeasonStatus.Draft);

        var detail = await Client.GetFromJsonAsync<LeagueDetailDto>($"/api/public/leagues/{leagueId}");

        detail!.Seasons.Should().ContainSingle(s => s.Id == seasonId);
        detail.Seasons.Should().NotContain(s => s.Status == "Draft");
    }

    [Fact]
    public async Task Standings_ReflectPlayedResults_Anonymously()
    {
        var (leagueId, seasonId, divisionId, teamA, _) = await SeedPlayedSeason();

        var tables = await Client.GetFromJsonAsync<TableDto[]>(
            $"/api/public/leagues/{leagueId}/seasons/{seasonId}/standings");

        var table = tables!.Single(t => t.DivisionId == divisionId);
        table.Rows.Length.Should().Be(2);
        table.Rows[0].TeamId.Should().Be(teamA); // winner on top
        table.Rows[0].Points.Should().Be(2);
        table.Rows[0].RubbersFor.Should().Be(9);
    }

    [Fact]
    public async Task Fixtures_AreListedAnonymously_WithScores()
    {
        var (leagueId, seasonId, _, _, _) = await SeedPlayedSeason();

        var fixtures = await Client.GetFromJsonAsync<FixtureDto[]>(
            $"/api/public/leagues/{leagueId}/seasons/{seasonId}/fixtures");

        fixtures!.Should().ContainSingle();
        fixtures[0].HomeTeamName.Should().Be("Acme 1");
        fixtures[0].AwayTeamName.Should().Be("Beta 1");
        fixtures[0].Status.Should().Be("Played");
        fixtures[0].HomeScore.Should().Be(9);
    }

    [Fact]
    public async Task Standings_ForSeasonNotInLeague_Returns404()
    {
        var (_, seasonId, _, _, _) = await SeedPlayedSeason();

        var response = await Client.GetAsync(
            $"/api/public/leagues/{Guid.NewGuid()}/seasons/{seasonId}/standings");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
