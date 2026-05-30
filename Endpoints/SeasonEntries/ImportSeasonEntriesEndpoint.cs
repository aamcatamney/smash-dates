using System.Security.Claims;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;
using smash_dates.Services.Import;

namespace smash_dates.Endpoints.SeasonEntries;

// Bulk-import season entries from CSV (columns: team, division), resolving names within the
// league. LeagueAdmin only; season must be Draft. Partial import + upsert by (season, team):
// a team already entered is moved to the row's division. Teams come from clubs with an
// accepted membership; team gender must match the division.
public static class ImportSeasonEntriesEndpoint
{
    public static IEndpointRouteBuilder MapImportSeasonEntriesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/import", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid leagueId,
        Guid seasonId,
        CsvImportRequest request,
        ClaimsPrincipal principal,
        ISeasonRepository seasons,
        IDivisionRepository divisions,
        ITeamRepository teams,
        ISeasonEntryRepository entries,
        ILeagueAdminRepository leagueAdmins,
        CancellationToken ct)
    {
        var season = await seasons.GetByIdAsync(seasonId, ct);
        if (season is null || season.LeagueId != leagueId) return Results.NotFound();

        var authz = await LeagueAuthorizer.RequireLeagueAdminAsync(principal, leagueId, leagueAdmins, ct);
        if (authz is not null) return authz;

        if (season.Status != SeasonStatus.Draft)
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Teams can only be assigned while the season is Draft");

        var doc = CsvParser.Parse(request.Csv ?? string.Empty);
        if (CsvImportSupport.RequireColumns(doc, "team", "division") is { } missing) return missing;

        var divisionsByName = (await divisions.ListByLeagueAsync(leagueId, ct))
            .GroupBy(d => d.Name.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        // Team names are unique per club, not per league, so flag ambiguous names rather than
        // guessing which club's team was meant.
        var teamGroups = (await teams.ListByLeagueAcceptedMembersAsync(leagueId, ct))
            .GroupBy(t => t.Name.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new ImportResult();
        foreach (var row in doc.Rows)
        {
            var teamName = row.Get("team");
            var divisionName = row.Get("division");
            if (teamName.Length == 0 || divisionName.Length == 0)
            {
                result.Error(row.LineNumber, "Both team and division are required");
                continue;
            }

            if (!teamGroups.TryGetValue(teamName.ToLowerInvariant(), out var matches))
            {
                result.Error(row.LineNumber, $"No team '{teamName}' in an accepted member club");
                continue;
            }
            if (matches.Count > 1)
            {
                result.Error(row.LineNumber, $"Team name '{teamName}' is ambiguous across clubs");
                continue;
            }
            var team = matches[0];

            if (!divisionsByName.TryGetValue(divisionName.ToLowerInvariant(), out var division))
            {
                result.Error(row.LineNumber, $"No division '{divisionName}' in this league");
                continue;
            }

            if (team.Gender != division.Gender)
            {
                result.Error(row.LineNumber, $"Team '{teamName}' ({team.Gender}) does not match division gender {division.Gender}");
                continue;
            }

            var created = await entries.UpsertAsync(seasonId, division.Id, team.Id, ct);
            if (created) result.Created++; else result.Updated++;
        }

        return Results.Ok(result);
    }
}
