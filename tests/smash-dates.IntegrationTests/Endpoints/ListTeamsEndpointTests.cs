using System.Net;
using System.Net.Http.Json;
using smash_dates.Models;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class ListTeamsEndpointTests : IntegrationTestBase
{
    public ListTeamsEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record TeamDto(Guid Id, Guid ClubId, string Name, string Gender);

    [Fact]
    public async Task Get_ReturnsTeamsForClub_AnyAuthenticatedUser()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.CreateTeamAsync(clubId, "Acme Mens 1", DivisionGender.Mens);
        await Seeder.CreateTeamAsync(clubId, "Acme Ladies 1", DivisionGender.Ladies);
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var teams = await Client.GetFromJsonAsync<TeamDto[]>($"/api/clubs/{clubId}/teams");

        teams.Should().NotBeNull();
        teams!.Length.Should().Be(2);
    }

    [Fact]
    public async Task Get_UnknownClub_ReturnsEmpty()
    {
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var teams = await Client.GetFromJsonAsync<TeamDto[]>($"/api/clubs/{Guid.NewGuid()}/teams");

        teams.Should().NotBeNull();
        teams!.Length.Should().Be(0);
    }

    [Fact]
    public async Task Get_Unauthenticated_Returns401()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");

        var response = await Client.GetAsync($"/api/clubs/{clubId}/teams");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
