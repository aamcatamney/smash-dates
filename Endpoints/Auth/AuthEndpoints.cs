using Microsoft.AspNetCore.Antiforgery;

namespace smash_dates.Endpoints.Auth;

public static class AuthEndpoints
{
    public const string RateLimitPolicy = "auth";
    public const string XsrfCookieName = "XSRF-TOKEN";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").RequireRateLimiting(RateLimitPolicy);

        group.MapLoginEndpoint();
        group.MapLogoutEndpoint();
        group.MapRegisterEndpoint();
        group.MapMeEndpoint();
        group.MapForgotPasswordEndpoint();
        group.MapResetPasswordEndpoint();
        group.MapChangePasswordEndpoint();
        group.MapVerifyEmailEndpoint();
        group.MapResendVerificationEndpoint();

        return app;
    }

    // Absolute link back into the SPA for an emailed token (reset / verification).
    internal static string AppLink(HttpContext http, string path, string token) =>
        $"{http.Request.Scheme}://{http.Request.Host}{path}?token={token}";

    internal static void IssueXsrfCookie(HttpContext http, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(http);
        if (tokens.RequestToken is null) return;

        http.Response.Cookies.Append(XsrfCookieName, tokens.RequestToken, new CookieOptions
        {
            HttpOnly = false,
            Secure = http.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
        });
    }
}
