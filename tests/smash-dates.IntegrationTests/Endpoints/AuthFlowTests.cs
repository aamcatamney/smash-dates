using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using smash_dates.Repositories;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

// Email verification gate + password reset flows. Tokens are minted directly via the repo
// (the real flow emails them to the outbox), so the endpoints can be exercised end to end.
public sealed class AuthFlowTests : IntegrationTestBase
{
    public AuthFlowTests(PostgresFixture fixture) : base(fixture) { }

    private const string Pwd = "correct-horse-battery";
    private sealed record VerifyRequiredDto(bool EmailVerificationRequired);

    private async Task<Guid> UserIdAsync(string email)
    {
        using var scope = Factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<IUserRepository>().GetByEmailAsync(email);
        return user!.Id;
    }

    private async Task<string> IssueTokenAsync(Guid userId, string purpose)
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAuthTokenRepository>()
            .IssueAsync(userId, purpose, TimeSpan.FromHours(1));
    }

    private Task<HttpResponseMessage> Login(string email, string password) =>
        Client.PostAsJsonAsync("/api/auth/login", new { email, password });

    [Fact]
    public async Task Register_NonBootstrapUser_RequiresVerification_AndLoginIsBlocked()
    {
        await Seeder.CreateSystemAdminUserAsync("admin@example.com", Pwd); // first user already exists

        var register = await Client.PostAsJsonAsync("/api/auth/register", new { email = "new@example.com", password = Pwd });

        register.StatusCode.Should().Be(HttpStatusCode.OK);
        (await register.Content.ReadFromJsonAsync<VerifyRequiredDto>())!.EmailVerificationRequired.Should().BeTrue();

        var login = await Login("new@example.com", Pwd);
        login.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task VerifyEmail_ThenLoginSucceeds()
    {
        await Seeder.CreateSystemAdminUserAsync("admin@example.com", Pwd);
        await Client.PostAsJsonAsync("/api/auth/register", new { email = "new@example.com", password = Pwd });
        var token = await IssueTokenAsync(await UserIdAsync("new@example.com"), "EmailVerification");

        var verify = await Client.PostAsJsonAsync("/api/auth/verify-email", new { token });

        verify.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Login("new@example.com", Pwd)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_ChangesPasswordAndOldOneStopsWorking()
    {
        var user = await Seeder.CreateUserAsync("u@example.com", "old-password-1234"); // seeder users are verified
        var token = await IssueTokenAsync(user.Id, "PasswordReset");

        var reset = await Client.PostAsJsonAsync("/api/auth/reset-password", new { token, password = "new-password-5678" });

        reset.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Login("u@example.com", "old-password-1234")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Login("u@example.com", "new-password-5678")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_InvalidToken_Returns400()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/reset-password", new { token = "not-a-token", password = "new-password-5678" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ForgotPassword_AlwaysReturnsOk()
    {
        await Seeder.CreateUserAsync("u@example.com", Pwd);

        (await Client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "u@example.com" })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "nobody@example.com" })).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
