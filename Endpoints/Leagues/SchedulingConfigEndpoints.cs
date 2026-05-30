using System.Security.Claims;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Leagues;

public static class SchedulingConfigEndpoints
{
    public sealed record SchedulingConfig(int SpreadWeight, int LegWeight, int MinGapDays, int? TargetGapDays);

    public static IEndpointRouteBuilder MapSchedulingConfigEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{id:guid}/scheduling-config", Get);
        app.MapPatch("/{id:guid}/scheduling-config", Update);
        return app;
    }

    private static async Task<IResult> Get(Guid id, ILeagueRepository leagues, CancellationToken ct)
    {
        var league = await leagues.GetByIdAsync(id, ct);
        return league is null
            ? Results.NotFound()
            : Results.Ok(new SchedulingConfig(league.SpreadWeight, league.LegWeight, league.MinGapDays, league.TargetGapDays));
    }

    private static async Task<IResult> Update(
        Guid id,
        SchedulingConfig request,
        ClaimsPrincipal principal,
        ILeagueRepository leagues,
        ILeagueAdminRepository leagueAdmins,
        CancellationToken ct)
    {
        if (await leagues.GetByIdAsync(id, ct) is null) return Results.NotFound();

        var authz = await LeagueAuthorizer.RequireLeagueAdminAsync(principal, id, leagueAdmins, ct);
        if (authz is not null) return authz;

        if (request.SpreadWeight < 0 || request.LegWeight < 0 || request.MinGapDays < 0 || request.TargetGapDays is < 0)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Weights and gaps must be non-negative");

        await leagues.UpdateSchedulingConfigAsync(
            id, request.SpreadWeight, request.LegWeight, request.MinGapDays, request.TargetGapDays, ct);
        return Results.NoContent();
    }
}
