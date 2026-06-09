using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using smash_dates.Endpoints.Auth;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

// Authenticated display-name update (PATCH /api/auth/me).
public sealed class UpdateProfileEndpointTests : IntegrationTestBase
{
    public UpdateProfileEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private const string Pwd = "correct-horse-battery";

    private sealed record UserDto(Guid Id, string Email, string? DisplayName, bool IsSystemAdmin);

    private async Task<string> LoginAndGetXsrfAsync()
    {
        await Seeder.CreateUserAsync("u@example.com", Pwd, displayName: "Old Name");
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "u@example.com", password = Pwd });
        var me = await Client.GetAsync("/api/auth/me");
        return me.GetSetCookieValue(AuthEndpoints.XsrfCookieName)!;
    }

    private Task<HttpResponseMessage> UpdateName(string? displayName, string? xsrf)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/me")
        {
            Content = JsonContent.Create(new { displayName }),
        };
        if (xsrf is not null) request.Headers.Add("X-XSRF-TOKEN", xsrf);
        return Client.SendAsync(request);
    }

    [Fact]
    public async Task Update_SetsTheName_AndMeReflectsIt()
    {
        var xsrf = await LoginAndGetXsrfAsync();

        var response = await UpdateName("Fresh Name", xsrf);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<UserDto>())!.DisplayName.Should().Be("Fresh Name");
        var me = await Client.GetFromJsonAsync<UserDto>("/api/auth/me");
        me!.DisplayName.Should().Be("Fresh Name");
    }

    [Fact]
    public async Task Update_BlankValue_ClearsTheNameToNull()
    {
        var xsrf = await LoginAndGetXsrfAsync();

        var response = await UpdateName("   ", xsrf);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.GetFromJsonAsync<UserDto>("/api/auth/me"))!.DisplayName.Should().BeNull();
    }

    [Fact]
    public async Task Update_TooLong_Returns400()
    {
        var xsrf = await LoginAndGetXsrfAsync();

        var response = await UpdateName(new string('x', 81), xsrf);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_Unauthenticated_Returns401()
    {
        var response = await UpdateName("Whoever", xsrf: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Update_AuthenticatedWithoutXsrf_Returns400()
    {
        await LoginAndGetXsrfAsync();

        var response = await UpdateName("Whoever", xsrf: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
