using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Auth;

public static class LoginEndpoint
{
    public sealed record LoginRequest(string Email, string Password, bool RememberMe);
    public sealed record UserResponse(Guid Id, string Email, string? DisplayName, bool IsSystemAdmin);

    public static IEndpointRouteBuilder MapLoginEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/login", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        LoginRequest request,
        HttpContext http,
        IUserRepository users,
        IPasswordHasher hasher,
        IAntiforgery antiforgery,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Auth.Login");
        var email = (request.Email ?? string.Empty).Trim();
        var ip = http.Connection.RemoteIpAddress?.ToString();

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(request.Password))
        {
            logger.LogWarning("Login failed (missing fields). Email={Email} IP={Ip}", email, ip);
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials");
        }

        var user = await users.GetByEmailAsync(email, ct);
        if (user is null || !user.IsActive || !hasher.Verify(request.Password, user.PasswordHash))
        {
            logger.LogWarning("Login failed. Email={Email} IP={Ip}", email, ip);
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials");
        }

        // Credentials are valid but the email hasn't been verified — block login and let the
        // client offer to resend the verification email.
        if (!user.EmailVerified)
        {
            logger.LogWarning("Login blocked (unverified email). UserId={UserId}", user.Id);
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Email not verified");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
        };
        if (user.IsSystemAdmin)
        {
            claims.Add(new Claim(AuthorizationPolicies.SystemAdminClaim, "true"));
        }
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        var props = new AuthenticationProperties
        {
            IsPersistent = request.RememberMe,
            ExpiresUtc = request.RememberMe ? DateTimeOffset.UtcNow.AddDays(14) : null,
        };

        await http.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            props);

        AuthEndpoints.IssueXsrfCookie(http, antiforgery);

        logger.LogInformation("Login success. UserId={UserId}", user.Id);

        return Results.Ok(new UserResponse(user.Id, user.Email, user.DisplayName, user.IsSystemAdmin));
    }
}
