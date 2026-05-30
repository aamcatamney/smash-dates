using System.Net;
using System.Net.Http.Json;
using smash_dates.Models;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

// The mid-season block deferred since slice 2b: Withdraw/Expel are forbidden while the
// club has a team entered in a non-Closed season of the league.
public sealed class MidSeasonMembershipBlockEndpointTests : IntegrationTestBase
{
    public MidSeasonMembershipBlockEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record Ctx(Guid LeagueId, Guid MembershipId, Guid ClubId, User SysAdmin, User ClubAdmin);

    private async Task<Ctx> Arrange(bool withOpenEntry, SeasonStatus seasonStatus = SeasonStatus.Proposed)
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        var clubAdmin = await Seeder.CreateUserAsync("ca@example.com", "correct-horse-battery");
        await Seeder.GrantClubAdminAsync(clubId, clubAdmin.Id, clubAdmin.Id);
        var membershipId = await Seeder.CreateMembershipAsync(clubId, leagueId, MembershipStatus.Accepted);

        if (withOpenEntry)
        {
            var seasonId = await Seeder.CreateSeasonAsync(leagueId, "2025/26", new DateOnly(2025, 9, 1), new DateOnly(2026, 4, 30), seasonStatus);
            var divisionId = await Seeder.CreateDivisionAsync(leagueId, "Mens 1", DivisionGender.Mens, 1, 9);
            var teamId = await Seeder.CreateTeamAsync(clubId, "Acme 1", DivisionGender.Mens);
            await Seeder.CreateSeasonEntryAsync(seasonId, divisionId, teamId);
        }

        return new Ctx(leagueId, membershipId, clubId, sys, clubAdmin);
    }

    [Fact]
    public async Task Withdraw_WhenClubHasOpenSeasonEntry_Returns409()
    {
        var c = await Arrange(withOpenEntry: true);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "ca@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsync($"/api/leagues/{c.LeagueId}/memberships/{c.MembershipId}/withdraw", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Withdraw_WhenNoOpenSeasonEntry_Succeeds()
    {
        var c = await Arrange(withOpenEntry: false);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "ca@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsync($"/api/leagues/{c.LeagueId}/memberships/{c.MembershipId}/withdraw", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Expel_WhenClubHasOpenSeasonEntry_Returns409()
    {
        var c = await Arrange(withOpenEntry: true);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsync($"/api/leagues/{c.LeagueId}/memberships/{c.MembershipId}/expel", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Withdraw_WhenEntryOnlyInClosedSeason_Succeeds()
    {
        var c = await Arrange(withOpenEntry: true, seasonStatus: SeasonStatus.Closed);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "ca@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsync($"/api/leagues/{c.LeagueId}/memberships/{c.MembershipId}/withdraw", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
