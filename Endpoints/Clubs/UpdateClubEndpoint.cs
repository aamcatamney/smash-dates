using System.Net.Mail;
using System.Security.Claims;
using Npgsql;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Clubs;

public static class UpdateClubEndpoint
{
    private const int MaxNameLength = 200;
    private const int MaxNotesLength = 4000;
    private const string DuplicateSqlState = "23505";
    private const string CheckViolationSqlState = "23514";

    public sealed record UpdateClubRequest(string Name, string ShortCode, string ContactEmail, string? Notes);

    public static IEndpointRouteBuilder MapUpdateClubEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/{id:guid}", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        UpdateClubRequest request,
        ClaimsPrincipal principal,
        IClubRepository clubs,
        IClubAdminRepository clubAdmins,
        CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(id, ct) is null) return Results.NotFound();

        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, id, clubAdmins, ct);
        if (authz is not null) return authz;

        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0 || name.Length > MaxNameLength)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid name");

        var shortCode = (request.ShortCode ?? string.Empty).Trim().ToUpperInvariant();
        if (shortCode.Length < 3 || shortCode.Length > 5
            || !System.Text.RegularExpressions.Regex.IsMatch(shortCode, "^[A-Z0-9]+$"))
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid ShortCode");

        var email = (request.ContactEmail ?? string.Empty).Trim();
        if (email.Length == 0 || !MailAddress.TryCreate(email, out _))
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid contact email");

        var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes!.Trim();
        if (notes is { Length: > MaxNotesLength })
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Notes too long");

        try
        {
            await clubs.UpdateAsync(id, name, shortCode, email, notes, ct);
            return Results.NoContent();
        }
        catch (PostgresException ex) when (ex.SqlState == DuplicateSqlState)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Name or ShortCode already in use");
        }
        catch (PostgresException ex) when (ex.SqlState == CheckViolationSqlState)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "ShortCode failed schema constraint");
        }
    }
}
