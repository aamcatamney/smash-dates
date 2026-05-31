using smash_dates.Repositories;

namespace smash_dates.Endpoints.Auth;

// Consumes a one-time EmailVerification token and marks the account verified.
public static class VerifyEmailEndpoint
{
    public sealed record VerifyEmailRequest(string Token);

    public static IEndpointRouteBuilder MapVerifyEmailEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/verify-email", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        VerifyEmailRequest request,
        IAuthTokenRepository tokens,
        IUserRepository users,
        CancellationToken ct)
    {
        var userId = await tokens.ConsumeAsync(request.Token ?? string.Empty, "EmailVerification", ct);
        if (userId is null)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid or expired verification link");

        await users.SetEmailVerifiedAsync(userId.Value, ct);
        return Results.Ok();
    }
}
