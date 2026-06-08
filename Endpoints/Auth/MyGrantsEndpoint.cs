using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Auth;

// Read-only list of the signed-in user's role grants: SystemAdmin plus every LeagueAdmin@League,
// ClubAdmin@Club and SessionHost@Club, naming the relevant league/club. Informational only —
// granting and revoking live on the league/club admin screens, not here.
public static class MyGrantsEndpoint
{
    public sealed record Grant(Guid Id, string Name);

    public sealed record GrantsResponse(
        bool SystemAdmin,
        IReadOnlyList<Grant> LeagueAdmin,
        IReadOnlyList<Grant> ClubAdmin,
        IReadOnlyList<Grant> SessionHost);

    public static IEndpointRouteBuilder MapMyGrantsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/me/grants", Handle).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> Handle(
        HttpContext http,
        IUserRepository users,
        ILeagueAdminRepository leagueAdmins,
        IClubAdminRepository clubAdmins,
        ISessionHostRepository sessionHosts,
        CancellationToken ct)
    {
        if (http.User.UserId() is not { } userId)
            return Results.Unauthorized();

        var user = await users.GetByIdAsync(userId, ct);
        if (user is null || !user.IsActive)
            return Results.Unauthorized();

        var leagues = await leagueAdmins.ListByUserAsync(userId, ct);
        var clubs = await clubAdmins.ListByUserAsync(userId, ct);
        var hosted = await sessionHosts.ListByUserAsync(userId, ct);

        return Results.Ok(new GrantsResponse(
            user.IsSystemAdmin,
            leagues.Select(l => new Grant(l.LeagueId, l.LeagueName)).ToList(),
            clubs.Select(c => new Grant(c.ClubId, c.ClubName)).ToList(),
            hosted.Select(h => new Grant(h.ClubId, h.ClubName)).ToList()));
    }
}
