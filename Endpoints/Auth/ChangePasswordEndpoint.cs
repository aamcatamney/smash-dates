using Microsoft.AspNetCore.Antiforgery;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Auth;

// Authenticated self-service password change: verify the signed-in user's current password,
// then set a new one. Distinct from the logged-out, email-link reset flow (ResetPasswordEndpoint),
// which this leaves untouched.
public static class ChangePasswordEndpoint
{
    private const int MinPasswordLength = 12;

    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    public static IEndpointRouteBuilder MapChangePasswordEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/change-password", Handle).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> Handle(
        ChangePasswordRequest request,
        HttpContext http,
        IAntiforgery antiforgery,
        IUserRepository users,
        IPasswordHasher hasher,
        CancellationToken ct)
    {
        // CSRF protection for a sensitive, cookie-authenticated mutation (mirrors logout).
        try
        {
            await antiforgery.ValidateRequestAsync(http);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid antiforgery token");
        }

        if (http.User.UserId() is not { } userId)
            return Results.Unauthorized();

        if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length < MinPasswordLength)
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid password",
                detail: $"Password must be at least {MinPasswordLength} characters.");

        var user = await users.GetByIdAsync(userId, ct);
        if (user is null || !user.IsActive)
            return Results.Unauthorized();

        // Re-authenticate with the current password before allowing the change.
        if (!hasher.Verify(request.CurrentPassword ?? string.Empty, user.PasswordHash))
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Current password is incorrect");

        await users.UpdatePasswordAsync(userId, hasher.Hash(request.NewPassword), ct);
        return Results.Ok();
    }
}
