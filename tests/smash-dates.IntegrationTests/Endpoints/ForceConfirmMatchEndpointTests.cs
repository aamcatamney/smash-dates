using System.Net;
using System.Net.Http.Json;
using smash_dates.Models;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class ForceConfirmMatchEndpointTests : IntegrationTestBase
{
    public ForceConfirmMatchEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record ActionResult(string Status);

    private Task Login(string email) =>
        Client.PostAsJsonAsync("/api/auth/login", new { email, password = "correct-horse-battery" });

    [Fact]
    public async Task LeagueAdmin_ForceConfirms()
    {
        var s = await Seeder.CreateProposedMatchAsync("a@x.test", "b@x.test", "correct-horse-battery");
        var la = await Seeder.CreateUserAsync("la@x.test", "correct-horse-battery");
        await Seeder.GrantLeagueAdminAsync(s.LeagueId, la.Id);
        await Login("la@x.test");

        var response = await Client.PostAsync($"/api/matches/{s.MatchId}/force-confirm", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ActionResult>();
        result!.Status.Should().Be("Confirmed");
    }

    [Fact]
    public async Task SystemAdmin_ForceConfirms()
    {
        var s = await Seeder.CreateProposedMatchAsync("a@x.test", "b@x.test", "correct-horse-battery");
        await Login("sys-a@x.test");

        var response = await Client.PostAsync($"/api/matches/{s.MatchId}/force-confirm", null);

        var result = await response.Content.ReadFromJsonAsync<ActionResult>();
        result!.Status.Should().Be("Confirmed");
    }

    [Fact]
    public async Task ClubAdminNotLeagueAdmin_Returns403()
    {
        var s = await Seeder.CreateProposedMatchAsync("a@x.test", "b@x.test", "correct-horse-battery");
        await Login("a@x.test"); // club admin, not league admin

        var response = await Client.PostAsync($"/api/matches/{s.MatchId}/force-confirm", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UnknownMatch_Returns404()
    {
        await Client.LoginAsSystemAdminAsync("sys@x.test", "correct-horse-battery", Seeder);

        var response = await Client.PostAsync($"/api/matches/{Guid.NewGuid()}/force-confirm", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ForceNonProposed_Returns409()
    {
        var s = await Seeder.CreateProposedMatchAsync("a@x.test", "b@x.test", "correct-horse-battery", MatchStatus.Rejected);
        await Login("sys-a@x.test");

        var response = await Client.PostAsync($"/api/matches/{s.MatchId}/force-confirm", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
