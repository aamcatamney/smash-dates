using System.Net;
using System.Net.Http.Json;
using smash_dates.Models;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class AcceptMatchEndpointTests : IntegrationTestBase
{
    public AcceptMatchEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record AcceptResult(string Status, bool HomeAccepted, bool AwayAccepted);

    private Task Login(string email) =>
        Client.PostAsJsonAsync("/api/auth/login", new { email, password = "correct-horse-battery" });

    [Fact]
    public async Task HomeClubAdmin_Accepts_StaysProposed()
    {
        var s = await Seeder.CreateProposedMatchAsync("a@x.test", "b@x.test", "correct-horse-battery");
        await Login("a@x.test");

        var response = await Client.PostAsync($"/api/matches/{s.MatchId}/accept", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<AcceptResult>();
        result!.Status.Should().Be("Proposed");
        result.HomeAccepted.Should().BeTrue();
        result.AwayAccepted.Should().BeFalse();
    }

    [Fact]
    public async Task BothClubsAccept_BecomesConfirmed()
    {
        var s = await Seeder.CreateProposedMatchAsync("a@x.test", "b@x.test", "correct-horse-battery");
        await Login("a@x.test");
        await Client.PostAsync($"/api/matches/{s.MatchId}/accept", null);
        await Login("b@x.test");

        var response = await Client.PostAsync($"/api/matches/{s.MatchId}/accept", null);

        var result = await response.Content.ReadFromJsonAsync<AcceptResult>();
        result!.Status.Should().Be("Confirmed");
    }

    [Fact]
    public async Task DerbyMatch_SingleAdminAccept_Confirms()
    {
        var s = await Seeder.CreateProposedMatchAsync("a@x.test", "b@x.test", "correct-horse-battery", sameClub: true);
        await Login("a@x.test");

        var response = await Client.PostAsync($"/api/matches/{s.MatchId}/accept", null);

        var result = await response.Content.ReadFromJsonAsync<AcceptResult>();
        result!.Status.Should().Be("Confirmed");
    }

    [Fact]
    public async Task NonClubAdmin_Returns403()
    {
        var s = await Seeder.CreateProposedMatchAsync("a@x.test", "b@x.test", "correct-horse-battery");
        await Client.LoginAsAsync("plain@x.test", "correct-horse-battery", Seeder);

        var response = await Client.PostAsync($"/api/matches/{s.MatchId}/accept", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UnknownMatch_Returns404()
    {
        await Client.LoginAsSystemAdminAsync("sys@x.test", "correct-horse-battery", Seeder);

        var response = await Client.PostAsync($"/api/matches/{Guid.NewGuid()}/accept", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AcceptNonProposed_Returns409()
    {
        var s = await Seeder.CreateProposedMatchAsync("a@x.test", "b@x.test", "correct-horse-battery", MatchStatus.Rejected);
        await Login("a@x.test");

        var response = await Client.PostAsync($"/api/matches/{s.MatchId}/accept", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
