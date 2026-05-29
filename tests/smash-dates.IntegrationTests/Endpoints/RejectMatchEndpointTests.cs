using System.Net;
using System.Net.Http.Json;
using smash_dates.Models;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class RejectMatchEndpointTests : IntegrationTestBase
{
    public RejectMatchEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record ActionResult(string Status);

    private Task Login(string email) =>
        Client.PostAsJsonAsync("/api/auth/login", new { email, password = "correct-horse-battery" });

    [Fact]
    public async Task AwayClubAdmin_Rejects_BecomesRejected()
    {
        var s = await Seeder.CreateProposedMatchAsync("a@x.test", "b@x.test", "correct-horse-battery");
        await Login("b@x.test");

        var response = await Client.PostAsync($"/api/matches/{s.MatchId}/reject", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ActionResult>();
        result!.Status.Should().Be("Rejected");
    }

    [Fact]
    public async Task NonClubAdmin_Returns403()
    {
        var s = await Seeder.CreateProposedMatchAsync("a@x.test", "b@x.test", "correct-horse-battery");
        await Client.LoginAsAsync("plain@x.test", "correct-horse-battery", Seeder);

        var response = await Client.PostAsync($"/api/matches/{s.MatchId}/reject", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UnknownMatch_Returns404()
    {
        await Client.LoginAsSystemAdminAsync("sys@x.test", "correct-horse-battery", Seeder);

        var response = await Client.PostAsync($"/api/matches/{Guid.NewGuid()}/reject", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RejectConfirmed_Returns409()
    {
        var s = await Seeder.CreateProposedMatchAsync("a@x.test", "b@x.test", "correct-horse-battery", MatchStatus.Confirmed);
        await Login("a@x.test");

        var response = await Client.PostAsync($"/api/matches/{s.MatchId}/reject", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
