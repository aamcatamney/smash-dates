using System.Net;
using System.Net.Http.Json;
using smash_dates.Endpoints.Auth;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class MeEndpointTests : IntegrationTestBase
{
    public MeEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Me_Unauthenticated_Returns401()
    {
        var response = await Client.GetAsync("/api/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_AfterLogin_ReturnsUser()
    {
        await Seeder.CreateUserAsync("me@example.com", "correct-horse-battery", "Me");
        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "me@example.com",
            password = "correct-horse-battery",
            rememberMe = false,
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await Client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LoginEndpoint.UserResponse>();
        body!.Email.Should().Be("me@example.com");
        body.DisplayName.Should().Be("Me");
    }
}
