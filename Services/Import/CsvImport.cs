using Microsoft.AspNetCore.Http;

namespace smash_dates.Services.Import;

// Request body shared by every CSV import endpoint. The client reads the chosen file as
// text and posts it here, so imports ride the existing JSON + antiforgery pipeline (no
// multipart/file-upload plumbing).
public sealed record CsvImportRequest(string Csv);

public static class CsvImportSupport
{
    // 400 problem if any required column is absent (case-insensitive), else null.
    public static IResult? RequireColumns(CsvDocument doc, params string[] required)
    {
        var present = doc.Headers.Select(h => h.ToLowerInvariant()).ToHashSet();
        var missing = required.Where(r => !present.Contains(r.ToLowerInvariant())).ToList();
        if (missing.Count == 0) return null;
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: $"CSV missing required column(s): {string.Join(", ", missing)}");
    }
}
