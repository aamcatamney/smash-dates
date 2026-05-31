using smash_dates.Repositories;

namespace smash_dates.Endpoints.Auth;

// Always returns 200 (no account enumeration). If the email maps to an active user, a
// one-time reset link is enqueued to the outbox.
public static class ForgotPasswordEndpoint
{
    public sealed record ForgotPasswordRequest(string Email);

    public static IEndpointRouteBuilder MapForgotPasswordEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/forgot-password", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        ForgotPasswordRequest request,
        HttpContext http,
        IUserRepository users,
        IAuthTokenRepository tokens,
        INotificationRepository outbox,
        CancellationToken ct)
    {
        var email = (request.Email ?? string.Empty).Trim();
        var user = await users.GetByEmailAsync(email, ct);
        if (user is { IsActive: true })
        {
            var token = await tokens.IssueAsync(user.Id, "PasswordReset", TimeSpan.FromHours(1), ct);
            var link = AuthEndpoints.AppLink(http, "/reset-password", token);
            await outbox.EnqueueAsync(
                user.Email,
                "Reset your smash-dates password",
                $"A password reset was requested for your account. Open this link within 1 hour to set a new password:\n{link}\n\nIf you didn't request this, you can ignore this email.",
                ct);
        }

        return Results.Ok();
    }
}
