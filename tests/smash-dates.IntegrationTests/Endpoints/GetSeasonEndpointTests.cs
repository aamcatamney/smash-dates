using System.Net;
using System.Net.Http.Json;
using smash_dates.Models;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class GetSeasonEndpointTests : IntegrationTestBase
{
    public GetSeasonEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record WeekDto(string StartDate, string EndDate, string WeekType);
    private sealed record SeasonDetailDto(
        Guid Id, Guid LeagueId, string Name, string StartDate, string EndDate, string Status, WeekDto[] Weeks);

    [Fact]
    public async Task Get_ReturnsSeasonWithWeeks_OrderedByStartDate()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        var seasonId = await Seeder.CreateSeasonAsync(leagueId, "2025/26", new DateOnly(2025, 9, 1), new DateOnly(2026, 4, 30));
        await Seeder.CreateSeasonWeekAsync(seasonId, new DateOnly(2025, 9, 8), new DateOnly(2025, 9, 14), WeekType.Mixed);
        await Seeder.CreateSeasonWeekAsync(seasonId, new DateOnly(2025, 9, 1), new DateOnly(2025, 9, 7), WeekType.Level);
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var season = await Client.GetFromJsonAsync<SeasonDetailDto>($"/api/leagues/{leagueId}/seasons/{seasonId}");

        season.Should().NotBeNull();
        season!.Weeks.Length.Should().Be(2);
        season.Weeks[0].StartDate.Should().Be("2025-09-01");
        season.Weeks[0].WeekType.Should().Be("Level");
        season.Status.Should().Be("Draft");
    }

    [Fact]
    public async Task Get_UnknownSeason_Returns404()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.GetAsync($"/api/leagues/{leagueId}/seasons/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_SeasonOfDifferentLeague_Returns404()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueA = await Seeder.CreateLeagueAsync("A", admin.Id);
        var leagueB = await Seeder.CreateLeagueAsync("B", admin.Id);
        var seasonInB = await Seeder.CreateSeasonAsync(leagueB, "2025/26", new DateOnly(2025, 9, 1), new DateOnly(2026, 4, 30));
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.GetAsync($"/api/leagues/{leagueA}/seasons/{seasonInB}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
