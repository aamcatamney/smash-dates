using System.Net;
using System.Net.Http.Json;
using smash_dates.Models;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class SchedulingDiagnosticsEndpointTests : IntegrationTestBase
{
    public SchedulingDiagnosticsEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record DivisionDiag(
        Guid DivisionId, string DivisionName, int Teams, int MatchesRequired, int MatchesPlaced, int EligibleWeeks);
    private sealed record UnplacedDto(string DivisionName, string HomeTeamName, string AwayTeamName);
    private sealed record Diagnostics(
        bool FullyPlaced, int TotalRequired, int TotalPlaced, DivisionDiag[] Divisions, UnplacedDto[] Unplaced);

    // A Mens division with two teams from two accepted clubs (each with a venue), plus one week
    // of the given type. SystemAdmin is seeded; pass loggedIn to sign in as them.
    private async Task<(Guid League, Guid Season)> ArrangeAsync(WeekType weekType)
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        var seasonId = await Seeder.CreateSeasonAsync(leagueId, "2025/26", new DateOnly(2025, 9, 1), new DateOnly(2025, 9, 30), SeasonStatus.Draft);
        await Seeder.CreateSeasonWeekAsync(seasonId, new DateOnly(2025, 9, 1), new DateOnly(2025, 9, 7), weekType);
        var divisionId = await Seeder.CreateDivisionAsync(leagueId, "Mens 1", DivisionGender.Mens, 1, 9);

        var clubA = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.CreateMembershipAsync(clubA, leagueId, MembershipStatus.Accepted);
        await Seeder.CreateVenueAsync(clubA, "Acme Hall", 1);
        var teamA = await Seeder.CreateTeamAsync(clubA, "Acme 1", DivisionGender.Mens);
        await Seeder.CreateSeasonEntryAsync(seasonId, divisionId, teamA);

        var clubB = await Seeder.CreateClubAsync("Beta", "BETA");
        await Seeder.CreateMembershipAsync(clubB, leagueId, MembershipStatus.Accepted);
        await Seeder.CreateVenueAsync(clubB, "Beta Hall", 1);
        var teamB = await Seeder.CreateTeamAsync(clubB, "Beta 1", DivisionGender.Mens);
        await Seeder.CreateSeasonEntryAsync(seasonId, divisionId, teamB);

        return (leagueId, seasonId);
    }

    private Task LoginSystemAdmin() =>
        Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

    [Fact]
    public async Task Diagnose_FeasibleSeason_ReportsFullyPlaced()
    {
        var (leagueId, seasonId) = await ArrangeAsync(WeekType.Level);
        await LoginSystemAdmin();

        var d = await Client.GetFromJsonAsync<Diagnostics>(
            $"/api/leagues/{leagueId}/seasons/{seasonId}/scheduling-diagnostics");

        d!.FullyPlaced.Should().BeTrue();
        d.Unplaced.Should().BeEmpty();
        var div = d.Divisions.Single();
        div.Teams.Should().Be(2);
        div.MatchesRequired.Should().Be(2); // double round-robin for 2 teams
        div.MatchesPlaced.Should().Be(2);
        div.EligibleWeeks.Should().Be(1);
    }

    [Fact]
    public async Task Diagnose_NoEligibleWeeks_ReportsUnplacedPairings()
    {
        // A Mens division but only a Mixed week → nothing can be placed.
        var (leagueId, seasonId) = await ArrangeAsync(WeekType.Mixed);
        await LoginSystemAdmin();

        var d = await Client.GetFromJsonAsync<Diagnostics>(
            $"/api/leagues/{leagueId}/seasons/{seasonId}/scheduling-diagnostics");

        d!.FullyPlaced.Should().BeFalse();
        d.Divisions.Single().EligibleWeeks.Should().Be(0);
        d.Divisions.Single().MatchesPlaced.Should().Be(0);
        d.Unplaced.Should().HaveCount(2); // home + away leg
        d.Unplaced.Should().OnlyContain(u => u.DivisionName == "Mens 1");
    }

    [Fact]
    public async Task Diagnose_AsNonAdmin_Returns403()
    {
        var (leagueId, seasonId) = await ArrangeAsync(WeekType.Level);
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.GetAsync(
            $"/api/leagues/{leagueId}/seasons/{seasonId}/scheduling-diagnostics");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
