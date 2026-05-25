using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class UpdateClubEndpointTests : IntegrationTestBase
{
    public UpdateClubEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private static HttpRequestMessage Patch(string url, object body)
    {
        return new HttpRequestMessage(HttpMethod.Patch, url)
        {
            Content = JsonContent.Create(body),
        };
    }

    [Fact]
    public async Task Patch_AsClubAdmin_Updates_Returns204()
    {
        var admin = await Seeder.CreateUserAsync("admin@example.com", "correct-horse-battery");
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.GrantClubAdminAsync(clubId, admin.Id, admin.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "correct-horse-battery" });

        var response = await Client.SendAsync(Patch($"/api/clubs/{clubId}", new
        {
            name = "Acme Renamed",
            shortCode = "ACMER",
            contactEmail = "contact@acme.test",
            notes = (string?)null,
        }));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Patch_AsNonAdmin_Returns403()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.SendAsync(Patch($"/api/clubs/{clubId}", new
        {
            name = "X", shortCode = "XYZ", contactEmail = "x@y.test", notes = (string?)null,
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
