using System.Net.Mail;
using smash_dates.Repositories;

namespace smash_dates.Endpoints.Users;

public static class LookupUserEndpoint
{
    private const int MaxEmailLength = 254;

    public sealed record UserLookupResponse(Guid Id, string Email, string? DisplayName);

    public static IEndpointRouteBuilder MapLookupUserEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/lookup", Handle);
        return app;
    }

    private static async Task<IResult> Handle(string? email, IUserRepository users, CancellationToken ct)
    {
        var trimmed = (email ?? string.Empty).Trim();
        if (trimmed.Length == 0 || trimmed.Length > MaxEmailLength || !MailAddress.TryCreate(trimmed, out _))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid email");
        }

        var user = await users.GetByEmailAsync(trimmed, ct);
        return user is null
            ? Results.NotFound()
            : Results.Ok(new UserLookupResponse(user.Id, user.Email, user.DisplayName));
    }
}
