using System.Security.Claims;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;
using smash_dates.Services.Import;

namespace smash_dates.Endpoints.Players;

// Bulk-import a club's players from CSV (columns: name, gender, grade?). Each row creates a NEW
// player and links a Member affiliation to this club. There is no reuse-by-name: duplicate
// identities across clubs are reconciled later by a separate merge, not matched on import.
public static class ImportClubPlayersEndpoint
{
    private const int MaxNameLength = 200;

    public static IEndpointRouteBuilder MapImportClubPlayersEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/import", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId,
        CsvImportRequest request,
        ClaimsPrincipal principal,
        IClubRepository clubs,
        IPlayerRepository players,
        IClubAdminRepository clubAdmins,
        CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(clubId, ct) is null) return Results.NotFound();

        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, clubId, clubAdmins, ct);
        if (authz is not null) return authz;

        var doc = CsvParser.Parse(request.Csv ?? string.Empty);
        if (CsvImportSupport.RequireColumns(doc, "name", "gender") is { } missing) return missing;

        var result = new ImportResult();
        foreach (var row in doc.Rows)
        {
            var name = row.Get("name");
            if (name.Length == 0 || name.Length > MaxNameLength)
            {
                result.Error(row.LineNumber, "Invalid name");
                continue;
            }

            if (!Enum.TryParse<Gender>(row.Get("gender"), ignoreCase: true, out var gender))
            {
                result.Error(row.LineNumber, "Gender must be Male or Female");
                continue;
            }

            var gradeText = row.Get("grade");
            int? grade = null;
            if (gradeText.Length > 0)
            {
                if (!int.TryParse(gradeText, out var g) || g is < 1 or > 5)
                {
                    result.Error(row.LineNumber, $"Invalid grade '{gradeText}' (must be 1-5)");
                    continue;
                }
                grade = g;
            }

            var playerId = await players.CreateAsync(name, gender, ct);
            await players.LinkAsync(playerId, clubId, PlayerClubType.Member, ct);
            if (grade is not null)
                await players.SetGradeAsync(playerId, grade, ct);

            result.Created++;
        }

        return Results.Ok(result);
    }
}
