using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using smash_dates.Endpoints.Auth;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

// Authenticated self-service password change. Distinct from the logged-out reset flow.
public sealed class ChangePasswordEndpointTests : IntegrationTestBase
{
    public ChangePasswordEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private const string OldPwd = "old-password-1234";
    private const string NewPwd = "new-password-5678";

    private async Task<string> LoginAndGetXsrfAsync(string email, string password)
    {
        await Client.PostAsJsonAsync("/api/auth/login", new { email, password, rememberMe = false });
        var me = await Client.GetAsync("/api/auth/me");
        return me.GetSetCookieValue(AuthEndpoints.XsrfCookieName)!;
    }

    private Task<HttpResponseMessage> ChangePassword(string currentPassword, string newPassword, string? xsrf)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/change-password")
        {
            Content = JsonContent.Create(new { currentPassword, newPassword }),
        };
        if (xsrf is not null) request.Headers.Add("X-XSRF-TOKEN", xsrf);
        return Client.SendAsync(request);
    }

    private Task<HttpResponseMessage> Login(string email, string password) =>
        Client.PostAsJsonAsync("/api/auth/login", new { email, password });

    [Fact]
    public async Task ChangePassword_WithCorrectCurrent_Succeeds_AndNewPasswordWorks()
    {
        await Seeder.CreateUserAsync("u@example.com", OldPwd); // seeder users are verified
        var xsrf = await LoginAndGetXsrfAsync("u@example.com", OldPwd);

        var response = await ChangePassword(OldPwd, NewPwd, xsrf);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // Old password no longer signs in; the new one does.
        (await Login("u@example.com", OldPwd)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Login("u@example.com", NewPwd)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrent_Returns400_AndPasswordUnchanged()
    {
        await Seeder.CreateUserAsync("u@example.com", OldPwd);
        var xsrf = await LoginAndGetXsrfAsync("u@example.com", OldPwd);

        var response = await ChangePassword("not-my-password", NewPwd, xsrf);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        // The original password still works.
        (await Login("u@example.com", OldPwd)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_NewTooShort_Returns400()
    {
        await Seeder.CreateUserAsync("u@example.com", OldPwd);
        var xsrf = await LoginAndGetXsrfAsync("u@example.com", OldPwd);

        var response = await ChangePassword(OldPwd, "too-short", xsrf);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_Unauthenticated_Returns401()
    {
        var response = await ChangePassword(OldPwd, NewPwd, xsrf: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePassword_AuthenticatedWithoutXsrf_Returns400()
    {
        await Seeder.CreateUserAsync("u@example.com", OldPwd);
        await Login("u@example.com", OldPwd);

        var response = await ChangePassword(OldPwd, NewPwd, xsrf: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
