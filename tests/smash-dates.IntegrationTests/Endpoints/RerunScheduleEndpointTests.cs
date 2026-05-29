using System.Net;
using System.Net.Http.Json;
using smash_dates.Models;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class RerunScheduleEndpointTests : IntegrationTestBase
{
    public RerunScheduleEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record Setup(Guid LeagueId, Guid SeasonId, Guid ConfirmedMatchId, Guid RejectedMatchId);
    private sealed record MatchDto(Guid Id, string Status);

    // A Proposed season (two teams, two Level weeks) with one Confirmed leg and one Rejected leg.
    private async Task<Setup> Arrange(bool enoughDates = true)
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        var seasonId = await Seeder.CreateSeasonAsync(leagueId, "2025/26", new DateOnly(2025, 9, 1), new DateOnly(2025, 9, 30), SeasonStatus.Proposed);
        await Seeder.CreateSeasonWeekAsync(seasonId, new DateOnly(2025, 9, 1), new DateOnly(2025, 9, 1), WeekType.Level);
        if (enoughDates)
            await Seeder.CreateSeasonWeekAsync(seasonId, new DateOnly(2025, 9, 8), new DateOnly(2025, 9, 8), WeekType.Level);
        var divisionId = await Seeder.CreateDivisionAsync(leagueId, "Mens 1", DivisionGender.Mens, 1, 9);

        var clubA = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.CreateMembershipAsync(clubA, leagueId, MembershipStatus.Accepted);
        var venueA = await Seeder.CreateVenueAsync(clubA, "Acme Hall", 1);
        var teamA = await Seeder.CreateTeamAsync(clubA, "Acme 1", DivisionGender.Mens);
        await Seeder.CreateSeasonEntryAsync(seasonId, divisionId, teamA);

        var clubB = await Seeder.CreateClubAsync("Beta", "BETA");
        await Seeder.CreateMembershipAsync(clubB, leagueId, MembershipStatus.Accepted);
        var venueB = await Seeder.CreateVenueAsync(clubB, "Beta Hall", 1);
        var teamB = await Seeder.CreateTeamAsync(clubB, "Beta 1", DivisionGender.Mens);
        await Seeder.CreateSeasonEntryAsync(seasonId, divisionId, teamB);

        var confirmed = await Seeder.CreateMatchAsync(seasonId, divisionId, teamA, teamB, venueA, new DateOnly(2025, 9, 1), MatchStatus.Confirmed, true, true);
        var rejected = await Seeder.CreateMatchAsync(seasonId, divisionId, teamB, teamA, venueB, new DateOnly(2025, 9, 1), MatchStatus.Rejected);
        return new Setup(leagueId, seasonId, confirmed, rejected);
    }

    private Task LoginSystemAdmin() =>
        Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

    [Fact]
    public async Task Rerun_Feasible_PreservesConfirmed_ReplacesRejected()
    {
        var s = await Arrange();
        await LoginSystemAdmin();

        var response = await Client.PostAsync($"/api/leagues/{s.LeagueId}/seasons/{s.SeasonId}/rerun", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var matches = await Client.GetFromJsonAsync<MatchDto[]>($"/api/leagues/{s.LeagueId}/seasons/{s.SeasonId}/matches");
        matches!.Length.Should().Be(2);
        matches.Count(m => m.Status == "Confirmed").Should().Be(1);
        matches.Count(m => m.Status == "Proposed").Should().Be(1);
        matches.Should().Contain(m => m.Id == s.ConfirmedMatchId); // confirmed leg untouched
        matches.Should().NotContain(m => m.Id == s.RejectedMatchId); // rejected leg replaced
    }

    [Fact]
    public async Task Rerun_AsLeagueAdmin_Returns200()
    {
        var s = await Arrange();
        var la = await Seeder.CreateUserAsync("la@example.com", "correct-horse-battery");
        await Seeder.GrantLeagueAdminAsync(s.LeagueId, la.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "la@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsync($"/api/leagues/{s.LeagueId}/seasons/{s.SeasonId}/rerun", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Rerun_AsNonAdmin_Returns403()
    {
        var s = await Arrange();
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsync($"/api/leagues/{s.LeagueId}/seasons/{s.SeasonId}/rerun", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Rerun_SeasonNotProposed_Returns409()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        var seasonId = await Seeder.CreateSeasonAsync(leagueId, "2025/26", new DateOnly(2025, 9, 1), new DateOnly(2025, 9, 30), SeasonStatus.Draft);
        await LoginSystemAdmin();

        var response = await Client.PostAsync($"/api/leagues/{leagueId}/seasons/{seasonId}/rerun", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Rerun_UnknownSeason_Returns404()
    {
        var s = await Arrange();
        await LoginSystemAdmin();

        var response = await Client.PostAsync($"/api/leagues/{s.LeagueId}/seasons/{Guid.NewGuid()}/rerun", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Rerun_Infeasible_Returns422_PersistsNothing()
    {
        var s = await Arrange(enoughDates: false); // only the date taken by the confirmed leg
        await LoginSystemAdmin();

        var response = await Client.PostAsync($"/api/leagues/{s.LeagueId}/seasons/{s.SeasonId}/rerun", null);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var matches = await Client.GetFromJsonAsync<MatchDto[]>($"/api/leagues/{s.LeagueId}/seasons/{s.SeasonId}/matches");
        matches.Should().Contain(m => m.Id == s.RejectedMatchId); // unchanged on failure
    }
}
