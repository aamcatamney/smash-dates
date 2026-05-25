using System.Net;
using System.Net.Http.Json;
using smash_dates.Endpoints.Memberships;
using smash_dates.IntegrationTests.Infrastructure;
using smash_dates.Models;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class ListClubMembershipsEndpointTests : IntegrationTestBase
{
    public ListClubMembershipsEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Get_ReturnsMembershipsForClub()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueA = await Seeder.CreateLeagueAsync("A", sys.Id);
        var leagueB = await Seeder.CreateLeagueAsync("B", sys.Id);
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.CreateMembershipAsync(clubId, leagueA, MembershipStatus.Accepted, sys.Id);
        await Seeder.CreateMembershipAsync(clubId, leagueB, MembershipStatus.Pending, sys.Id);
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.GetAsync($"/api/clubs/{clubId}/memberships");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListClubMembershipsEndpoint.ClubMembershipSummary[]>();
        body!.Should().HaveCount(2);
    }
}
