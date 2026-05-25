using smash_dates.Repositories;

namespace smash_dates.Endpoints.Memberships;

public static class ListLeagueMembershipsEndpoint
{
    public sealed record MembershipSummary(
        Guid Id,
        Guid ClubId,
        Guid LeagueId,
        string Status,
        DateTime InvitedAt,
        DateTime? RespondedAt);

    public static IEndpointRouteBuilder MapListLeagueMembershipsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid leagueId,
        ILeagueRepository leagues,
        IClubLeagueMembershipRepository memberships,
        CancellationToken ct)
    {
        if (await leagues.GetByIdAsync(leagueId, ct) is null) return Results.NotFound();

        var rows = await memberships.ListByLeagueAsync(leagueId, ct);
        return Results.Ok(rows.Select(m => new MembershipSummary(
            m.Id, m.ClubId, m.LeagueId, m.Status.ToString(), m.InvitedAt, m.RespondedAt)).ToArray());
    }
}
