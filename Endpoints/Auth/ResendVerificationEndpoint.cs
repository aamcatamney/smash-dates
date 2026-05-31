using smash_dates.Repositories;

namespace smash_dates.Endpoints.Auth;

// Always returns 200. If the email maps to an active, unverified user, a fresh verification
// link is enqueued.
public static class ResendVerificationEndpoint
{
    public sealed record ResendVerificationRequest(string Email);

    public static IEndpointRouteBuilder MapResendVerificationEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/resend-verification", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        ResendVerificationRequest request,
        HttpContext http,
        IUserRepository users,
        IAuthTokenRepository tokens,
        INotificationRepository outbox,
        CancellationToken ct)
    {
        var user = await users.GetByEmailAsync((request.Email ?? string.Empty).Trim(), ct);
        if (user is { IsActive: true, EmailVerified: false })
        {
            var token = await tokens.IssueAsync(user.Id, "EmailVerification", TimeSpan.FromDays(7), ct);
            var link = AuthEndpoints.AppLink(http, "/verify-email", token);
            await outbox.EnqueueAsync(
                user.Email,
                "Verify your smash-dates email",
                $"Confirm your email address to finish setting up your account:\n{link}",
                ct);
        }

        return Results.Ok();
    }
}
