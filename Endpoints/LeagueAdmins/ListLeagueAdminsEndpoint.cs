using smash_dates.Repositories;

namespace smash_dates.Endpoints.LeagueAdmins;

public static class ListLeagueAdminsEndpoint
{
    public sealed record LeagueAdminSummary(Guid UserId, string Email, string? DisplayName, DateTime GrantedAt);

    public static IEndpointRouteBuilder MapListLeagueAdminsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid leagueId,
        ILeagueRepository leagues,
        ILeagueAdminRepository admins,
        IUserRepository users,
        CancellationToken ct)
    {
        var league = await leagues.GetByIdAsync(leagueId, ct);
        if (league is null) return Results.NotFound();

        var grants = await admins.ListByLeagueAsync(leagueId, ct);
        var summaries = new List<LeagueAdminSummary>(grants.Count);
        foreach (var grant in grants)
        {
            var user = await users.GetByIdAsync(grant.UserId, ct);
            if (user is null) continue;
            summaries.Add(new LeagueAdminSummary(user.Id, user.Email, user.DisplayName, grant.GrantedAt));
        }
        return Results.Ok(summaries);
    }
}
