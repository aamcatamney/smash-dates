using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;
using smash_dates.Models;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class ExpelMembershipEndpointTests : IntegrationTestBase
{
    public ExpelMembershipEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Post_AsLeagueAdmin_ExpelsAccepted_Returns204()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        await Seeder.GrantLeagueAdminAsync(leagueId, sys.Id, sys.Id);
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        var membershipId = await Seeder.CreateMembershipAsync(clubId, leagueId, MembershipStatus.Accepted, sys.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsync($"/api/leagues/{leagueId}/memberships/{membershipId}/expel", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Post_AsNonLeagueAdmin_Returns403()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        var membershipId = await Seeder.CreateMembershipAsync(clubId, leagueId, MembershipStatus.Accepted, sys.Id);
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsync($"/api/leagues/{leagueId}/memberships/{membershipId}/expel", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
