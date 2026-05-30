using System.Net;
using System.Net.Http.Json;
using smash_dates.Models;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class RecordResultEndpointTests : IntegrationTestBase
{
    public RecordResultEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record MatchDetail(string Status, int? HomeScore, int? AwayScore, string? PlayedOn, bool IsWalkover);

    private Task Login(string email) =>
        Client.PostAsJsonAsync("/api/auth/login", new { email, password = "correct-horse-battery" });

    [Fact]
    public async Task ClubAdmin_RecordsResult_MatchPlayed()
    {
        var s = await Seeder.CreateProposedMatchAsync("a@x.test", "b@x.test", "correct-horse-battery", MatchStatus.Confirmed);
        await Login("a@x.test");

        var response = await Client.PostAsJsonAsync($"/api/matches/{s.MatchId}/result",
            new { homeScore = 6, awayScore = 3, playedOn = "2025-09-03" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await Client.GetFromJsonAsync<MatchDetail>($"/api/matches/{s.MatchId}");
        detail!.Status.Should().Be("Played");
        detail.HomeScore.Should().Be(6);
        detail.AwayScore.Should().Be(3);
        detail.IsWalkover.Should().BeFalse();
    }

    [Fact]
    public async Task ScoresNotSummingToRubbersPerMatch_Returns400()
    {
        var s = await Seeder.CreateProposedMatchAsync("a@x.test", "b@x.test", "correct-horse-battery", MatchStatus.Confirmed);
        await Login("a@x.test");

        var response = await Client.PostAsJsonAsync($"/api/matches/{s.MatchId}/result",
            new { homeScore = 6, awayScore = 2, playedOn = "2025-09-03" }); // 8 != 9

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PlayedOnBeforeMatchDate_Returns400()
    {
        var s = await Seeder.CreateProposedMatchAsync("a@x.test", "b@x.test", "correct-horse-battery", MatchStatus.Confirmed);
        await Login("a@x.test");

        var response = await Client.PostAsJsonAsync($"/api/matches/{s.MatchId}/result",
            new { homeScore = 5, awayScore = 4, playedOn = "2025-09-01" }); // before 2025-09-03

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MatchNotConfirmed_Returns409()
    {
        var s = await Seeder.CreateProposedMatchAsync("a@x.test", "b@x.test", "correct-horse-battery"); // Proposed
        await Login("a@x.test");

        var response = await Client.PostAsJsonAsync($"/api/matches/{s.MatchId}/result",
            new { homeScore = 5, awayScore = 4, playedOn = "2025-09-03" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task NonClubAdmin_Returns403()
    {
        var s = await Seeder.CreateProposedMatchAsync("a@x.test", "b@x.test", "correct-horse-battery", MatchStatus.Confirmed);
        await Client.LoginAsAsync("plain@x.test", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync($"/api/matches/{s.MatchId}/result",
            new { homeScore = 5, awayScore = 4, playedOn = "2025-09-03" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UnknownMatch_Returns404()
    {
        await Client.LoginAsSystemAdminAsync("sys@x.test", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync($"/api/matches/{Guid.NewGuid()}/result",
            new { homeScore = 5, awayScore = 4, playedOn = "2025-09-03" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
