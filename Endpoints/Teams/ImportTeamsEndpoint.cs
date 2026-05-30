using System.Security.Claims;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;
using smash_dates.Services.Import;

namespace smash_dates.Endpoints.Teams;

// Bulk-import a club's teams from CSV (columns: name, gender). Partial import: each row is
// validated independently and reported. A team that already exists is left unchanged unless
// the row's gender differs (gender is immutable), which is an error.
public static class ImportTeamsEndpoint
{
    private const int MaxNameLength = 200;

    public static IEndpointRouteBuilder MapImportTeamsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/import", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId,
        CsvImportRequest request,
        ClaimsPrincipal principal,
        IClubRepository clubs,
        ITeamRepository teams,
        IClubAdminRepository clubAdmins,
        CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(clubId, ct) is null) return Results.NotFound();

        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, clubId, clubAdmins, ct);
        if (authz is not null) return authz;

        var doc = CsvParser.Parse(request.Csv ?? string.Empty);
        if (CsvImportSupport.RequireColumns(doc, "name", "gender") is { } missing) return missing;

        var existing = (await teams.ListByClubAsync(clubId, ct))
            .GroupBy(t => t.Name.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        var result = new ImportResult();
        foreach (var row in doc.Rows)
        {
            var name = row.Get("name");
            if (name.Length == 0 || name.Length > MaxNameLength)
            {
                result.Error(row.LineNumber, "Invalid name");
                continue;
            }

            if (!Enum.TryParse<DivisionGender>(row.Get("gender"), ignoreCase: false, out var gender))
            {
                result.Error(row.LineNumber, $"Invalid gender '{row.Get("gender")}' (expected Mens, Ladies or Mixed)");
                continue;
            }

            if (existing.TryGetValue(name.ToLowerInvariant(), out var team))
            {
                if (team.Gender != gender)
                {
                    result.Error(row.LineNumber, $"Team '{name}' already exists as {team.Gender}; gender is immutable");
                    continue;
                }
                result.Updated++; // already present with the same gender — nothing to change
                continue;
            }

            await teams.CreateAsync(clubId, name, gender, ct);
            result.Created++;
        }

        return Results.Ok(result);
    }
}
