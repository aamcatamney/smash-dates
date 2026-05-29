using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class GetMatchEndpointTests : IntegrationTestBase
{
    public GetMatchEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record MatchDetail(
        Guid Id, Guid SeasonId, Guid DivisionId, string DivisionName,
        Guid HomeTeamId, string HomeTeamName, Guid AwayTeamId, string AwayTeamName,
        Guid VenueId, string VenueName, string MatchDate, string Status,
        bool HomeAccepted, bool AwayAccepted);

    [Fact]
    public async Task Get_ReturnsDetailWithNames_AnyAuthenticatedUser()
    {
        var s = await Seeder.CreateProposedMatchAsync("a@x.test", "b@x.test", "correct-horse-battery");
        await Client.LoginAsAsync("plain@x.test", "correct-horse-battery", Seeder);

        var detail = await Client.GetFromJsonAsync<MatchDetail>($"/api/matches/{s.MatchId}");

        detail.Should().NotBeNull();
        detail!.HomeTeamName.Length.Should().BeGreaterThan(0);
        detail.AwayTeamName.Length.Should().BeGreaterThan(0);
        detail.Status.Should().Be("Proposed");
    }

    [Fact]
    public async Task Get_UnknownMatch_Returns404()
    {
        await Client.LoginAsSystemAdminAsync("sys@x.test", "correct-horse-battery", Seeder);

        var response = await Client.GetAsync($"/api/matches/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_Unauthenticated_Returns401()
    {
        var s = await Seeder.CreateProposedMatchAsync("a@x.test", "b@x.test", "correct-horse-battery");

        var response = await Client.GetAsync($"/api/matches/{s.MatchId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
