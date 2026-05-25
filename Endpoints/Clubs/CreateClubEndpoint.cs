using System.Net.Mail;
using System.Security.Claims;
using Npgsql;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Clubs;

public static class CreateClubEndpoint
{
    private const int MaxNameLength = 200;
    private const int MaxNotesLength = 4000;
    private const string DuplicateSqlState = "23505";
    private const string CheckViolationSqlState = "23514";
    private const string ForeignKeyViolationSqlState = "23503";

    public sealed record CreateClubRequest(
        string Name,
        string ShortCode,
        string ContactEmail,
        string? Notes,
        Guid FirstClubAdminUserId);

    public sealed record ClubResponse(Guid Id, string Name, string ShortCode, string ContactEmail, string? Notes);

    public static IEndpointRouteBuilder MapCreateClubEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/", Handle)
            .RequireAuthorization(AuthorizationPolicies.SystemAdmin);
        return app;
    }

    private static async Task<IResult> Handle(
        CreateClubRequest request,
        ClaimsPrincipal principal,
        IClubRepository clubs,
        CancellationToken ct)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0 || name.Length > MaxNameLength)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid name");

        var shortCode = (request.ShortCode ?? string.Empty).Trim().ToUpperInvariant();
        if (shortCode.Length < 3 || shortCode.Length > 5)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "ShortCode must be 3-5 characters");
        if (!System.Text.RegularExpressions.Regex.IsMatch(shortCode, "^[A-Z0-9]+$"))
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "ShortCode must be ASCII letters/digits");

        var email = (request.ContactEmail ?? string.Empty).Trim();
        if (email.Length == 0 || !MailAddress.TryCreate(email, out _))
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid contact email");

        var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes!.Trim();
        if (notes is { Length: > MaxNotesLength })
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Notes too long");

        if (request.FirstClubAdminUserId == Guid.Empty)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "FirstClubAdminUserId is required");

        var grantedBy = principal.UserId()
            ?? throw new InvalidOperationException("Authenticated principal missing user id.");

        try
        {
            var id = await clubs.CreateWithFirstAdminAsync(
                name, shortCode, email, notes, request.FirstClubAdminUserId, grantedBy, ct);
            return Results.Created($"/api/clubs/{id}", new ClubResponse(id, name, shortCode, email, notes));
        }
        catch (PostgresException ex) when (ex.SqlState == DuplicateSqlState)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Name or ShortCode already in use");
        }
        catch (PostgresException ex) when (ex.SqlState == ForeignKeyViolationSqlState)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "FirstClubAdminUserId references unknown user");
        }
        catch (PostgresException ex) when (ex.SqlState == CheckViolationSqlState)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "ShortCode failed schema constraint");
        }
    }
}
