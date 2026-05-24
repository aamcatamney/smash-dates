using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class RevokeLeagueAdminEndpointTests : IntegrationTestBase
{
    public RevokeLeagueAdminEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Delete_RemovesNonLastAdmin_Returns204()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var second = await Seeder.CreateUserAsync("second@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        await Seeder.GrantLeagueAdminAsync(leagueId, sys.Id, sys.Id);
        await Seeder.GrantLeagueAdminAsync(leagueId, second.Id, sys.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.DeleteAsync($"/api/leagues/{leagueId}/admins/{second.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_LastNonSystemAdmin_BlockedWith409()
    {
        var soleAdmin = await Seeder.CreateUserAsync("admin@example.com", "correct-horse-battery");
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        await Seeder.GrantLeagueAdminAsync(leagueId, soleAdmin.Id, sys.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "correct-horse-battery" });

        var response = await Client.DeleteAsync($"/api/leagues/{leagueId}/admins/{soleAdmin.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Delete_LastAdmin_ForcedBySystemAdmin_204()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var soleAdmin = await Seeder.CreateUserAsync("admin@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        await Seeder.GrantLeagueAdminAsync(leagueId, soleAdmin.Id, sys.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.DeleteAsync($"/api/leagues/{leagueId}/admins/{soleAdmin.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_NotAGrant_Returns404()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        await Seeder.GrantLeagueAdminAsync(leagueId, sys.Id, sys.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.DeleteAsync($"/api/leagues/{leagueId}/admins/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_AsNonAdmin_Returns403()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        await Seeder.GrantLeagueAdminAsync(leagueId, sys.Id, sys.Id);
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.DeleteAsync($"/api/leagues/{leagueId}/admins/{sys.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
