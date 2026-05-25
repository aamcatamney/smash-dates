namespace smash_dates.Endpoints.Memberships;

public static class MembershipEndpoints
{
    public static IEndpointRouteBuilder MapMembershipEndpoints(this IEndpointRouteBuilder app)
    {
        // League-scoped: invite + list-by-league + state transitions
        var league = app.MapGroup("/api/leagues/{leagueId:guid}/memberships")
            .RequireAuthorization();
        league.MapInviteMembershipEndpoint();
        league.MapListLeagueMembershipsEndpoint();
        league.MapAcceptMembershipEndpoint();
        league.MapDeclineMembershipEndpoint();
        league.MapWithdrawMembershipEndpoint();
        league.MapExpelMembershipEndpoint();

        // Club-scoped: list-by-club only
        var club = app.MapGroup("/api/clubs/{clubId:guid}/memberships")
            .RequireAuthorization();
        club.MapListClubMembershipsEndpoint();
        return app;
    }
}
