using System.Security.Claims;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;
using smash_dates.Services.Import;

namespace smash_dates.Endpoints.Players;

// Bulk-import a club's players from CSV (columns: name, gender, grade?, useExisting?). Each row
// links a Member affiliation to this club. `useExisting` (default false) reuses an existing global
// player with the same name+gender instead of creating a new one; an ambiguous match is an error.
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

            var useExistingText = row.Get("useExisting");
            var useExisting = false;
            if (useExistingText.Length > 0 && !bool.TryParse(useExistingText, out useExisting))
            {
                result.Error(row.LineNumber, $"Invalid useExisting '{useExistingText}' (true/false)");
                continue;
            }

            Guid playerId;
            if (useExisting)
            {
                var matches = await players.FindByNameAndGenderAsync(name, gender, ct);
                if (matches.Count > 1)
                {
                    result.Error(row.LineNumber, $"Ambiguous: {matches.Count} existing players named '{name}' ({gender})");
                    continue;
                }
                playerId = matches.Count == 1 ? matches[0].Id : await players.CreateAsync(name, gender, ct);
            }
            else
            {
                playerId = await players.CreateAsync(name, gender, ct);
            }

            // A pre-existing affiliation to this club counts as an update, not a create.
            var alreadyLinked = await players.GetLinkAsync(playerId, clubId, ct) is not null;
            await players.LinkAsync(playerId, clubId, PlayerClubType.Member, ct);
            if (grade is not null)
                await players.SetGradeAsync(playerId, grade, ct);

            if (alreadyLinked) result.Updated++;
            else result.Created++;
        }

        return Results.Ok(result);
    }
}
