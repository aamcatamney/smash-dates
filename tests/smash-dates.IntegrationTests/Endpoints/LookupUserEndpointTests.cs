using System.Net;
using System.Net.Http.Json;
using smash_dates.Endpoints.Users;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class LookupUserEndpointTests : IntegrationTestBase
{
    public LookupUserEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        var response = await Client.GetAsync("/api/users/lookup?email=foo@bar.com");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_KnownEmail_ReturnsUser()
    {
        await Seeder.CreateUserAsync("target@example.com", "correct-horse-battery", displayName: "Target");
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.GetAsync("/api/users/lookup?email=target@example.com");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LookupUserEndpoint.UserLookupResponse>();
        body!.Email.Should().Be("target@example.com");
        body.DisplayName.Should().Be("Target");
    }

    [Fact]
    public async Task Get_UnknownEmail_Returns404()
    {
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);
        var response = await Client.GetAsync("/api/users/lookup?email=nobody@example.com");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_InvalidEmail_Returns400()
    {
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);
        var response = await Client.GetAsync("/api/users/lookup?email=not-an-email");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
