using System.Security.Claims;
using Npgsql;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Memberships;

public static class InviteMembershipEndpoint
{
    private const string DuplicateSqlState = "23505";
    private const string ForeignKeySqlState = "23503";

    public sealed record InviteRequest(Guid ClubId);
    public sealed record InviteResponse(Guid Id, Guid ClubId, Guid LeagueId, string Status);

    public static IEndpointRouteBuilder MapInviteMembershipEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid leagueId,
        InviteRequest request,
        ClaimsPrincipal principal,
        ILeagueRepository leagues,
        ILeagueAdminRepository leagueAdmins,
        IClubRepository clubs,
        IClubLeagueMembershipRepository memberships,
        CancellationToken ct)
    {
        if (await leagues.GetByIdAsync(leagueId, ct) is null) return Results.NotFound();

        var authz = await LeagueAuthorizer.RequireLeagueAdminAsync(principal, leagueId, leagueAdmins, ct);
        if (authz is not null) return authz;

        if (request.ClubId == Guid.Empty)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "clubId is required");
        if (await clubs.GetByIdAsync(request.ClubId, ct) is null)
            return Results.NotFound();

        var invitedBy = principal.UserId()!.Value;

        try
        {
            var id = await memberships.InviteAsync(request.ClubId, leagueId, invitedBy, ct);
            return Results.Created(
                $"/api/leagues/{leagueId}/memberships/{id}",
                new InviteResponse(id, request.ClubId, leagueId, "Pending"));
        }
        catch (PostgresException ex) when (ex.SqlState == DuplicateSqlState)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Active membership already exists",
                detail: "There is already a Pending or Accepted membership for this club in this league.");
        }
        catch (PostgresException ex) when (ex.SqlState == ForeignKeySqlState)
        {
            return Results.NotFound();
        }
    }
}
