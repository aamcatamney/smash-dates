using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class OpsEndpointsTests : IntegrationTestBase
{
    public OpsEndpointsTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record HealthDto(string Status);
    private sealed record VersionDto(string Version);

    [Fact]
    public async Task Health_IsAnonymousAndOk()
    {
        var response = await Client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<HealthDto>())!.Status.Should().Be("ok");
    }

    [Fact]
    public async Task Version_ReturnsAVersionString()
    {
        var response = await Client.GetAsync("/api/version");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<VersionDto>())!.Version.Should().NotBeNullOrWhiteSpace();
    }
}
