using System.Net.Mail;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Npgsql;
using smash_dates.Repositories;
using smash_dates.Services.Auth;
using smash_dates.Services.Import;

namespace smash_dates.Endpoints.Clubs;

// Bulk-import the club roster from CSV (columns: name, shortCode, contactEmail,
// firstAdminEmail, notes). SystemAdmin only. Partial import + upsert by shortCode: an
// existing club's name/email/notes are updated (admins untouched); a new club is created
// and its firstAdminEmail — which must be a registered user — is granted ClubAdmin.
public static partial class ImportClubsEndpoint
{
    private const int MaxNameLength = 200;
    private const int MaxNotesLength = 4000;
    private const string DuplicateSqlState = "23505";

    [GeneratedRegex("^[A-Z0-9]+$")]
    private static partial Regex ShortCodePattern();

    public static IEndpointRouteBuilder MapImportClubsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/import", Handle).RequireAuthorization(AuthorizationPolicies.SystemAdmin);
        return app;
    }

    private static async Task<IResult> Handle(
        CsvImportRequest request,
        ClaimsPrincipal principal,
        IClubRepository clubs,
        IUserRepository users,
        CancellationToken ct)
    {
        var grantedBy = principal.UserId()
            ?? throw new InvalidOperationException("Authenticated principal missing user id.");

        var doc = CsvParser.Parse(request.Csv ?? string.Empty);
        if (CsvImportSupport.RequireColumns(doc, "name", "shortCode", "contactEmail") is { } missing) return missing;

        var result = new ImportResult();
        foreach (var row in doc.Rows)
        {
            var name = row.Get("name");
            if (name.Length == 0 || name.Length > MaxNameLength)
            {
                result.Error(row.LineNumber, "Invalid name");
                continue;
            }

            var shortCode = row.Get("shortCode").ToUpperInvariant();
            if (shortCode.Length is < 3 or > 5 || !ShortCodePattern().IsMatch(shortCode))
            {
                result.Error(row.LineNumber, $"Invalid shortCode '{shortCode}' (3-5 letters/digits)");
                continue;
            }

            var email = row.Get("contactEmail");
            if (email.Length == 0 || !MailAddress.TryCreate(email, out _))
            {
                result.Error(row.LineNumber, "Invalid contactEmail");
                continue;
            }

            var notes = row.Get("notes");
            notes = notes.Length == 0 ? null : notes;
            if (notes is { Length: > MaxNotesLength })
            {
                result.Error(row.LineNumber, "Notes too long");
                continue;
            }

            var existing = await clubs.GetByShortCodeAsync(shortCode, ct);
            if (existing is not null)
            {
                await clubs.UpdateAsync(existing.Id, name, shortCode, email, notes, ct);
                result.Updated++;
                continue;
            }

            var adminEmail = row.Get("firstAdminEmail");
            if (adminEmail.Length == 0)
            {
                result.Error(row.LineNumber, "firstAdminEmail is required for a new club");
                continue;
            }
            var admin = await users.GetByEmailAsync(adminEmail, ct);
            if (admin is null)
            {
                result.Error(row.LineNumber, $"No registered user '{adminEmail}' for firstAdminEmail");
                continue;
            }

            try
            {
                await clubs.CreateWithFirstAdminAsync(name, shortCode, email, notes, admin.Id, grantedBy, ct);
                result.Created++;
            }
            catch (PostgresException ex) when (ex.SqlState == DuplicateSqlState)
            {
                result.Error(row.LineNumber, $"Club name '{name}' already in use");
            }
        }

        return Results.Ok(result);
    }
}
