using System.Net;
using System.Net.Http.Json;
using smash_dates.Models;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class PostponeMatchEndpointTests : IntegrationTestBase
{
    public PostponeMatchEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record MatchDetail(string Status, bool HomeAccepted, bool AwayAccepted);

    private Task Login(string email) =>
        Client.PostAsJsonAsync("/api/auth/login", new { email, password = "correct-horse-battery" });

    [Fact]
    public async Task LeagueAdmin_PostponesConfirmedInActive_BecomesProposed_AcceptanceCleared()
    {
        var s = await Seeder.CreateProposedMatchAsync("a@x.test", "b@x.test", "correct-horse-battery",
            MatchStatus.Confirmed, seasonStatus: SeasonStatus.Active);
        var la = await Seeder.CreateUserAsync("la@x.test", "correct-horse-battery");
        await Seeder.GrantLeagueAdminAsync(s.LeagueId, la.Id);
        await Login("la@x.test");

        var response = await Client.PostAsync($"/api/matches/{s.MatchId}/postpone", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await Client.GetFromJsonAsync<MatchDetail>($"/api/matches/{s.MatchId}");
        detail!.Status.Should().Be("Proposed");
        detail.HomeAccepted.Should().BeFalse();
        detail.AwayAccepted.Should().BeFalse();
    }

    [Fact]
    public async Task SeasonNotActive_Returns409()
    {
        var s = await Seeder.CreateProposedMatchAsync("a@x.test", "b@x.test", "correct-horse-battery",
            MatchStatus.Confirmed, seasonStatus: SeasonStatus.Proposed);
        await Login("sys-a@x.test");

        var response = await Client.PostAsync($"/api/matches/{s.MatchId}/postpone", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task MatchNotConfirmed_Returns409()
    {
        var s = await Seeder.CreateProposedMatchAsync("a@x.test", "b@x.test", "correct-horse-battery",
            MatchStatus.Proposed, seasonStatus: SeasonStatus.Active);
        await Login("sys-a@x.test");

        var response = await Client.PostAsync($"/api/matches/{s.MatchId}/postpone", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ClubAdminNotLeagueAdmin_Returns403()
    {
        var s = await Seeder.CreateProposedMatchAsync("a@x.test", "b@x.test", "correct-horse-battery",
            MatchStatus.Confirmed, seasonStatus: SeasonStatus.Active);
        await Login("a@x.test"); // club admin, not league admin

        var response = await Client.PostAsync($"/api/matches/{s.MatchId}/postpone", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UnknownMatch_Returns404()
    {
        await Client.LoginAsSystemAdminAsync("sys@x.test", "correct-horse-battery", Seeder);

        var response = await Client.PostAsync($"/api/matches/{Guid.NewGuid()}/postpone", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
