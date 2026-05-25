using System.Net;
using System.Net.Http.Json;
using smash_dates.Endpoints.Memberships;
using smash_dates.IntegrationTests.Infrastructure;
using smash_dates.Models;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class ListLeagueMembershipsEndpointTests : IntegrationTestBase
{
    public ListLeagueMembershipsEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Get_ReturnsMemberships()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        var clubA = await Seeder.CreateClubAsync("A", "AAA");
        var clubB = await Seeder.CreateClubAsync("B", "BBB");
        await Seeder.CreateMembershipAsync(clubA, leagueId, MembershipStatus.Accepted, sys.Id);
        await Seeder.CreateMembershipAsync(clubB, leagueId, MembershipStatus.Pending, sys.Id);
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.GetAsync($"/api/leagues/{leagueId}/memberships");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListLeagueMembershipsEndpoint.MembershipSummary[]>();
        body!.Select(m => m.Status).Should().BeEquivalentTo(new[] { "Accepted", "Pending" });
    }

    [Fact]
    public async Task Get_UnknownLeague_Returns404()
    {
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);
        var response = await Client.GetAsync($"/api/leagues/{Guid.NewGuid()}/memberships");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
