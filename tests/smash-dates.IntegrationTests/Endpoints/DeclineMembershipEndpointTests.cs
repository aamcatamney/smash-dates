using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;
using smash_dates.Models;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class DeclineMembershipEndpointTests : IntegrationTestBase
{
    public DeclineMembershipEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Post_AsClubAdmin_DeclinesPending_Returns204()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var clubAdmin = await Seeder.CreateUserAsync("ca@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.GrantClubAdminAsync(clubId, clubAdmin.Id, sys.Id);
        var membershipId = await Seeder.CreateMembershipAsync(clubId, leagueId, MembershipStatus.Pending, sys.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "ca@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsync($"/api/leagues/{leagueId}/memberships/{membershipId}/decline", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Post_AcceptedMembership_Returns409()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.GrantClubAdminAsync(clubId, sys.Id, sys.Id);
        var membershipId = await Seeder.CreateMembershipAsync(clubId, leagueId, MembershipStatus.Accepted, sys.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsync($"/api/leagues/{leagueId}/memberships/{membershipId}/decline", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
