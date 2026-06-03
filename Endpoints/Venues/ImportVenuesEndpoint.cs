using System.Security.Claims;
using smash_dates.Repositories;
using smash_dates.Services.Auth;
using smash_dates.Services.Import;

namespace smash_dates.Endpoints.Venues;

// Bulk-import a club's venues from CSV (columns: name, courts, maxConcurrentMatches). Partial
// import + upsert: an existing venue (matched by name) is updated; a new one is created.
public static class ImportVenuesEndpoint
{
    private const int MaxNameLength = 200;

    public static IEndpointRouteBuilder MapImportVenuesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/import", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId,
        CsvImportRequest request,
        ClaimsPrincipal principal,
        IClubRepository clubs,
        IVenueRepository venues,
        IClubAdminRepository clubAdmins,
        CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(clubId, ct) is null) return Results.NotFound();

        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, clubId, clubAdmins, ct);
        if (authz is not null) return authz;

        var doc = CsvParser.Parse(request.Csv ?? string.Empty);
        if (CsvImportSupport.RequireColumns(doc, "name", "courts", "maxConcurrentMatches") is { } missing) return missing;

        var existing = (await venues.ListByClubAsync(clubId, ct))
            .GroupBy(v => v.Name.ToLowerInvariant())
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

            var courtsText = row.Get("courts");
            var courts = courtsText.Length == 0 ? 2 : 0;
            if (courtsText.Length > 0 && !int.TryParse(courtsText, out courts))
            {
                result.Error(row.LineNumber, $"Invalid courts '{courtsText}'");
                continue;
            }
            if (courts < 1)
            {
                result.Error(row.LineNumber, "Courts must be at least 1");
                continue;
            }

            var maxText = row.Get("maxConcurrentMatches");
            var maxConcurrent = maxText.Length == 0 ? 1 : 0;
            if (maxText.Length > 0 && !int.TryParse(maxText, out maxConcurrent))
            {
                result.Error(row.LineNumber, $"Invalid maxConcurrentMatches '{maxText}'");
                continue;
            }
            if (maxConcurrent is not (1 or 2))
            {
                result.Error(row.LineNumber, "Max concurrent matches must be 1 or 2");
                continue;
            }

            if (existing.TryGetValue(name.ToLowerInvariant(), out var venue))
            {
                await venues.UpdateAsync(venue.Id, name, courts, maxConcurrent, ct);
                result.Updated++;
                continue;
            }

            await venues.CreateAsync(clubId, name, courts, maxConcurrent, ct);
            result.Created++;
        }

        return Results.Ok(result);
    }
}
