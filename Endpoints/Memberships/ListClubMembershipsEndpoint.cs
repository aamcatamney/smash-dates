using smash_dates.Repositories;

namespace smash_dates.Endpoints.Memberships;

public static class ListClubMembershipsEndpoint
{
    public sealed record ClubMembershipSummary(
        Guid Id,
        Guid ClubId,
        Guid LeagueId,
        string Status,
        DateTime InvitedAt,
        DateTime? RespondedAt);

    public static IEndpointRouteBuilder MapListClubMembershipsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId,
        IClubRepository clubs,
        IClubLeagueMembershipRepository memberships,
        CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(clubId, ct) is null) return Results.NotFound();

        var rows = await memberships.ListByClubAsync(clubId, ct);
        return Results.Ok(rows.Select(m => new ClubMembershipSummary(
            m.Id, m.ClubId, m.LeagueId, m.Status.ToString(), m.InvitedAt, m.RespondedAt)).ToArray());
    }
}
