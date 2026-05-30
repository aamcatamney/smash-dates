using System.Net;
using System.Net.Http.Json;
using smash_dates.Models;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class RecordWalkoverEndpointTests : IntegrationTestBase
{
    public RecordWalkoverEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record MatchDetail(string Status, int? HomeScore, int? AwayScore, bool IsWalkover);

    private Task Login(string email) =>
        Client.PostAsJsonAsync("/api/auth/login", new { email, password = "correct-horse-battery" });

    [Fact]
    public async Task HomeWalkover_AwardsMaxToHome()
    {
        var s = await Seeder.CreateProposedMatchAsync("a@x.test", "b@x.test", "correct-horse-battery", MatchStatus.Confirmed);
        await Login("a@x.test");

        var response = await Client.PostAsJsonAsync($"/api/matches/{s.MatchId}/walkover", new { winner = "Home" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await Client.GetFromJsonAsync<MatchDetail>($"/api/matches/{s.MatchId}");
        detail!.Status.Should().Be("Played");
        detail.HomeScore.Should().Be(9); // RubbersPerMatch
        detail.AwayScore.Should().Be(0);
        detail.IsWalkover.Should().BeTrue();
    }

    [Fact]
    public async Task AwayWalkover_AwardsMaxToAway()
    {
        var s = await Seeder.CreateProposedMatchAsync("a@x.test", "b@x.test", "correct-horse-battery", MatchStatus.Confirmed);
        await Login("b@x.test");

        var response = await Client.PostAsJsonAsync($"/api/matches/{s.MatchId}/walkover", new { winner = "Away" });

        var detail = await Client.GetFromJsonAsync<MatchDetail>($"/api/matches/{s.MatchId}");
        detail!.HomeScore.Should().Be(0);
        detail.AwayScore.Should().Be(9);
        detail.IsWalkover.Should().BeTrue();
    }

    [Fact]
    public async Task InvalidWinner_Returns400()
    {
        var s = await Seeder.CreateProposedMatchAsync("a@x.test", "b@x.test", "correct-horse-battery", MatchStatus.Confirmed);
        await Login("a@x.test");

        var response = await Client.PostAsJsonAsync($"/api/matches/{s.MatchId}/walkover", new { winner = "Nobody" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MatchNotConfirmed_Returns409()
    {
        var s = await Seeder.CreateProposedMatchAsync("a@x.test", "b@x.test", "correct-horse-battery"); // Proposed
        await Login("a@x.test");

        var response = await Client.PostAsJsonAsync($"/api/matches/{s.MatchId}/walkover", new { winner = "Home" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task NonClubAdmin_Returns403()
    {
        var s = await Seeder.CreateProposedMatchAsync("a@x.test", "b@x.test", "correct-horse-battery", MatchStatus.Confirmed);
        await Client.LoginAsAsync("plain@x.test", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync($"/api/matches/{s.MatchId}/walkover", new { winner = "Home" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
