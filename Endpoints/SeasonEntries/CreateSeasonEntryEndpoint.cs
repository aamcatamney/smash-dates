using System.Security.Claims;
using Npgsql;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.SeasonEntries;

public static class CreateSeasonEntryEndpoint
{
    private const string DuplicateSqlState = "23505";

    public sealed record CreateSeasonEntryRequest(Guid TeamId, Guid DivisionId);

    public sealed record SeasonEntryResponse(Guid Id, Guid SeasonId, Guid DivisionId, Guid TeamId);

    public static IEndpointRouteBuilder MapCreateSeasonEntryEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid leagueId,
        Guid seasonId,
        CreateSeasonEntryRequest request,
        ClaimsPrincipal principal,
        ISeasonRepository seasons,
        IDivisionRepository divisions,
        ITeamRepository teams,
        ISeasonEntryRepository entries,
        IClubLeagueMembershipRepository memberships,
        ILeagueAdminRepository leagueAdmins,
        CancellationToken ct)
    {
        var season = await seasons.GetByIdAsync(seasonId, ct);
        if (season is null || season.LeagueId != leagueId) return Results.NotFound();

        var authz = await LeagueAuthorizer.RequireLeagueAdminAsync(principal, leagueId, leagueAdmins, ct);
        if (authz is not null) return authz;

        if (season.Status != SeasonStatus.Draft)
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Teams can only be assigned while the season is Draft");

        var division = await divisions.GetByIdAsync(request.DivisionId, ct);
        if (division is null || division.LeagueId != leagueId) return Results.NotFound();

        var team = await teams.GetByIdAsync(request.TeamId, ct);
        if (team is null) return Results.NotFound();

        if (team.Gender != division.Gender)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Team gender does not match the division's gender");

        if (!await memberships.HasAcceptedMembershipAsync(team.ClubId, leagueId, ct))
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "The team's club is not an accepted member of this league");

        try
        {
            var id = await entries.CreateAsync(seasonId, request.DivisionId, request.TeamId, ct);
            return Results.Created(
                $"/api/leagues/{leagueId}/seasons/{seasonId}/entries/{id}",
                new SeasonEntryResponse(id, seasonId, request.DivisionId, request.TeamId));
        }
        catch (PostgresException ex) when (ex.SqlState == DuplicateSqlState)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "That team is already entered in this season");
        }
    }
}
