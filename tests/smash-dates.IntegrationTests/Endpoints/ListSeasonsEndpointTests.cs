using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class ListSeasonsEndpointTests : IntegrationTestBase
{
    public ListSeasonsEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record SeasonDto(Guid Id, Guid LeagueId, string Name, string StartDate, string EndDate, string Status);

    [Fact]
    public async Task Get_ReturnsSeasonsForLeague_AnyAuthenticatedUser()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        await Seeder.CreateSeasonAsync(leagueId, "2024/25", new DateOnly(2024, 9, 1), new DateOnly(2025, 4, 30));
        await Seeder.CreateSeasonAsync(leagueId, "2025/26", new DateOnly(2025, 9, 1), new DateOnly(2026, 4, 30));
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var seasons = await Client.GetFromJsonAsync<SeasonDto[]>($"/api/leagues/{leagueId}/seasons");

        seasons.Should().NotBeNull();
        seasons!.Length.Should().Be(2);
    }

    [Fact]
    public async Task Get_Unauthenticated_Returns401()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);

        var response = await Client.GetAsync($"/api/leagues/{leagueId}/seasons");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
