using System.Net;
using System.Net.Http.Json;
using smash_dates.Endpoints.ClubAdmins;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class ListClubAdminsEndpointTests : IntegrationTestBase
{
    public ListClubAdminsEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        var response = await Client.GetAsync($"/api/clubs/{Guid.NewGuid()}/admins");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_UnknownClub_Returns404()
    {
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);
        var response = await Client.GetAsync($"/api/clubs/{Guid.NewGuid()}/admins");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_ReturnsCurrentAdmins()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var second = await Seeder.CreateUserAsync("second@example.com", "correct-horse-battery", displayName: "Second");
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.GrantClubAdminAsync(clubId, sys.Id, sys.Id);
        await Seeder.GrantClubAdminAsync(clubId, second.Id, sys.Id);
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.GetAsync($"/api/clubs/{clubId}/admins");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListClubAdminsEndpoint.ClubAdminSummary[]>();
        body!.Select(a => a.Email).Should().BeEquivalentTo(new[] { "sys@example.com", "second@example.com" });
        body!.Should().Contain(a => a.DisplayName == "Second");
    }
}
