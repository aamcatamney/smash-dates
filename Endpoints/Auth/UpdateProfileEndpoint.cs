using Microsoft.AspNetCore.Antiforgery;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Auth;

// Authenticated self-service profile edit. Currently just the display name: a blank/whitespace
// value clears it (the UI then falls back to the email). Returns the refreshed user so the
// client can update its session (e.g. the header greeting).
public static class UpdateProfileEndpoint
{
    private const int MaxDisplayNameLength = 80;

    public sealed record UpdateProfileRequest(string? DisplayName);

    public static IEndpointRouteBuilder MapUpdateProfileEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/me", Handle).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> Handle(
        UpdateProfileRequest request,
        HttpContext http,
        IAntiforgery antiforgery,
        IUserRepository users,
        CancellationToken ct)
    {
        // CSRF protection for a cookie-authenticated mutation (mirrors logout / change-password).
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

        var displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim();
        if (displayName is { Length: > MaxDisplayNameLength })
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid display name",
                detail: $"Display name must be {MaxDisplayNameLength} characters or fewer.");

        await users.UpdateDisplayNameAsync(userId, displayName, ct);

        var user = await users.GetByIdAsync(userId, ct);
        if (user is null || !user.IsActive)
            return Results.Unauthorized();

        return Results.Ok(new LoginEndpoint.UserResponse(user.Id, user.Email, user.DisplayName, user.IsSystemAdmin));
    }
}
