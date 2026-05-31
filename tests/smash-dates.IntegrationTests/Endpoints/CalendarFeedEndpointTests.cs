using System.Net;
using System.Net.Http.Json;
using smash_dates.Models;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class CalendarFeedEndpointTests : IntegrationTestBase
{
    public CalendarFeedEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record FeedDto(string Url);

    private async Task<Guid> SeedClubWithAMatchAsync()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        var divisionId = await Seeder.CreateDivisionAsync(leagueId, "Mens 1", DivisionGender.Mens, 1, 9);
        var clubA = await Seeder.CreateClubAsync("Acme", "ACME", contactEmail: "a@test");
        var clubB = await Seeder.CreateClubAsync("Beta", "BETA", contactEmail: "b@test");
        var venue = await Seeder.CreateVenueAsync(clubA, "Riverside Hall", 1);
        var teamA = await Seeder.CreateTeamAsync(clubA, "Acme 1st", DivisionGender.Mens);
        var teamB = await Seeder.CreateTeamAsync(clubB, "Beta 1st", DivisionGender.Mens);
        var seasonId = await Seeder.CreateSeasonAsync(leagueId, "2025/26", new DateOnly(2025, 9, 1), new DateOnly(2026, 4, 30), SeasonStatus.Active);
        await Seeder.CreateMatchAsync(seasonId, divisionId, teamA, teamB, venue, new DateOnly(2025, 9, 13), MatchStatus.Confirmed, true, true);
        return clubA;
    }

    [Fact]
    public async Task ClubFeed_MintedThenFetchedAnonymously_ReturnsCalendarWithTheMatch()
    {
        var clubId = await SeedClubWithAMatchAsync();
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });
        var feed = await Client.GetFromJsonAsync<FeedDto>($"/api/calendar/club/{clubId}/url");

        // Fetch the .ics with a fresh, unauthenticated client (as a calendar app would).
        using var anon = Factory.CreateClient();
        var response = await anon.GetAsync(feed!.Url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/calendar");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("BEGIN:VCALENDAR");
        body.Should().Contain("BEGIN:VEVENT");
        body.Should().Contain("Acme 1st v Beta 1st");
        body.Should().Contain("Riverside Hall");
    }

    [Fact]
    public async Task Feed_InvalidToken_Returns404()
    {
        using var anon = Factory.CreateClient();

        var response = await anon.GetAsync("/api/calendar/not-a-real-token.ics");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MintUrl_RequiresAuthentication()
    {
        var clubId = await SeedClubWithAMatchAsync();

        var response = await Client.GetAsync($"/api/calendar/club/{clubId}/url");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
