using System.Security.Claims;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Teams;

// A Team's persistent squad. Adding a player enforces eligibility: the player must hold a
// Confirmed discipline registration at the team's club, in a league the team is currently
// entered in, with a gender matching the team (for Level). See CONTEXT.md "Team Squad".
public static class TeamSquadEndpoints
{
    public sealed record AddSquadMemberRequest(Guid PlayerId);

    public static IEndpointRouteBuilder MapTeamSquadEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/clubs/{clubId:guid}/teams/{teamId:guid}/players").RequireAuthorization();
        group.MapGet("/", List);
        group.MapPost("/", Add);
        group.MapDelete("/{playerId:guid}", Remove);
        return app;
    }

    // Team gender -> (discipline, required player gender). Mixed has no gender requirement.
    private static (Discipline Discipline, Gender? Required) MapGender(DivisionGender g) => g switch
    {
        DivisionGender.Mens => (Discipline.Level, Gender.Male),
        DivisionGender.Ladies => (Discipline.Level, Gender.Female),
        _ => (Discipline.Mixed, null),
    };

    private static async Task<IResult> List(
        Guid clubId, Guid teamId, ITeamRepository teams, ITeamPlayerRepository squad, CancellationToken ct)
    {
        var team = await teams.GetByIdAsync(teamId, ct);
        if (team is null || team.ClubId != clubId) return Results.NotFound();
        return Results.Ok(await squad.ListByTeamAsync(teamId, ct));
    }

    private static async Task<IResult> Add(
        Guid clubId,
        Guid teamId,
        AddSquadMemberRequest request,
        ClaimsPrincipal principal,
        IClubRepository clubs,
        ITeamRepository teams,
        IPlayerRepository players,
        IClubAdminRepository clubAdmins,
        ITeamPlayerRepository squad,
        CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(clubId, ct) is null) return Results.NotFound();
        var team = await teams.GetByIdAsync(teamId, ct);
        if (team is null || team.ClubId != clubId) return Results.NotFound();

        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, clubId, clubAdmins, ct);
        if (authz is not null) return authz;

        var player = await players.GetByIdAsync(request.PlayerId, ct);
        if (player is null) return Results.NotFound();

        var (discipline, required) = MapGender(team.Gender);
        if (required is { } rg && player.Gender != rg)
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: $"Player gender does not match a {team.Gender} team");

        if (!await squad.IsEligibleAsync(request.PlayerId, clubId, discipline, ct))
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: $"Player has no confirmed {discipline} registration at this club");

        await squad.AddAsync(teamId, request.PlayerId, ct);
        return Results.Created($"/api/clubs/{clubId}/teams/{teamId}/players", new { request.PlayerId });
    }

    private static async Task<IResult> Remove(
        Guid clubId,
        Guid teamId,
        Guid playerId,
        ClaimsPrincipal principal,
        ITeamRepository teams,
        IClubAdminRepository clubAdmins,
        ITeamPlayerRepository squad,
        CancellationToken ct)
    {
        var team = await teams.GetByIdAsync(teamId, ct);
        if (team is null || team.ClubId != clubId) return Results.NotFound();

        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, clubId, clubAdmins, ct);
        if (authz is not null) return authz;

        return await squad.RemoveAsync(teamId, playerId, ct) ? Results.NoContent() : Results.NotFound();
    }
}
