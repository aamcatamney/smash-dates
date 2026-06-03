using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class SchedulingConfigEndpointTests : IntegrationTestBase
{
    public SchedulingConfigEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record Config(int SpreadWeight, int LegWeight, int MinGapDays, int? TargetGapDays, int CourtsPerMatch);

    private static HttpRequestMessage Patch(string url, object body) =>
        new(HttpMethod.Patch, url) { Content = JsonContent.Create(body) };

    [Fact]
    public async Task Get_ReturnsDefaults_ForNewLeague()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var config = await Client.GetFromJsonAsync<Config>($"/api/leagues/{leagueId}/scheduling-config");

        config!.SpreadWeight.Should().Be(2);
        config.LegWeight.Should().Be(1);
        config.MinGapDays.Should().Be(7);
        config.TargetGapDays.Should().BeNull();
        config.CourtsPerMatch.Should().Be(2);
    }

    [Fact]
    public async Task Patch_AsLeagueAdmin_UpdatesConfig()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var patch = await Client.SendAsync(Patch($"/api/leagues/{leagueId}/scheduling-config",
            new { spreadWeight = 5, legWeight = 3, minGapDays = 10, targetGapDays = 14, courtsPerMatch = 3 }));
        patch.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var config = await Client.GetFromJsonAsync<Config>($"/api/leagues/{leagueId}/scheduling-config");
        config.Should().Be(new Config(5, 3, 10, 14, 3));
    }

    [Fact]
    public async Task Patch_InvalidCourtsPerMatch_Returns400()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.SendAsync(Patch($"/api/leagues/{leagueId}/scheduling-config",
            new { spreadWeight = 2, legWeight = 1, minGapDays = 7, targetGapDays = (int?)null, courtsPerMatch = 0 }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Patch_AsNonAdmin_Returns403()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.SendAsync(Patch($"/api/leagues/{leagueId}/scheduling-config",
            new { spreadWeight = 5, legWeight = 3, minGapDays = 10, targetGapDays = (int?)null, courtsPerMatch = 2 }));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Patch_NegativeWeight_Returns400()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.SendAsync(Patch($"/api/leagues/{leagueId}/scheduling-config",
            new { spreadWeight = -1, legWeight = 1, minGapDays = 7, targetGapDays = (int?)null, courtsPerMatch = 2 }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
