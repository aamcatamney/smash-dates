using smash_dates.Repositories;

namespace smash_dates.Endpoints.ClubAdmins;

public static class ListClubAdminsEndpoint
{
    public sealed record ClubAdminSummary(Guid UserId, string Email, string? DisplayName, DateTime GrantedAt);

    public static IEndpointRouteBuilder MapListClubAdminsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId,
        IClubRepository clubs,
        IClubAdminRepository admins,
        IUserRepository users,
        CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(clubId, ct) is null) return Results.NotFound();

        var grants = await admins.ListByClubAsync(clubId, ct);
        var summaries = new List<ClubAdminSummary>(grants.Count);
        foreach (var grant in grants)
        {
            var user = await users.GetByIdAsync(grant.UserId, ct);
            if (user is null) continue;
            summaries.Add(new ClubAdminSummary(user.Id, user.Email, user.DisplayName, grant.GrantedAt));
        }
        return Results.Ok(summaries);
    }
}
