using System.Security.Claims;
using smash_dates.Repositories;
using smash_dates.Services.Auth;
using smash_dates.Services.Scheduling;

namespace smash_dates.Endpoints.Matches;

// On-demand "explain my schedule": runs the scheduler as a dry run (no persistence) and reports
// per-division feasibility plus the pairings it couldn't place. League-admin only. See issue #68.
public static class SchedulingDiagnosticsEndpoint
{
    public static IEndpointRouteBuilder MapSchedulingDiagnosticsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/scheduling-diagnostics", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid leagueId,
        Guid seasonId,
        ClaimsPrincipal principal,
        ISeasonRepository seasons,
        ILeagueAdminRepository leagueAdmins,
        IScheduleGenerator generator,
        CancellationToken ct)
    {
        var season = await seasons.GetByIdAsync(seasonId, ct);
        if (season is null || season.LeagueId != leagueId) return Results.NotFound();

        var authz = await LeagueAuthorizer.RequireLeagueAdminAsync(principal, leagueId, leagueAdmins, ct);
        if (authz is not null) return authz;

        return Results.Ok(await generator.DiagnoseAsync(seasonId, ct));
    }
}
