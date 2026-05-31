using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Auth;

// Consumes a one-time PasswordReset token and sets a new password. Reaching the reset link
// proves email access, so the account is also marked verified.
public static class ResetPasswordEndpoint
{
    private const int MinPasswordLength = 12;

    public sealed record ResetPasswordRequest(string Token, string Password);

    public static IEndpointRouteBuilder MapResetPasswordEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/reset-password", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        ResetPasswordRequest request,
        IAuthTokenRepository tokens,
        IUserRepository users,
        IPasswordHasher hasher,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.Password) || request.Password.Length < MinPasswordLength)
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid password",
                detail: $"Password must be at least {MinPasswordLength} characters.");

        var userId = await tokens.ConsumeAsync(request.Token ?? string.Empty, "PasswordReset", ct);
        if (userId is null)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid or expired reset link");

        await users.UpdatePasswordAsync(userId.Value, hasher.Hash(request.Password), ct);
        await users.SetEmailVerifiedAsync(userId.Value, ct);
        return Results.Ok();
    }
}
