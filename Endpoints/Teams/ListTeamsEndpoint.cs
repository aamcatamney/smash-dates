using smash_dates.Repositories;

namespace smash_dates.Endpoints.Teams;

public static class ListTeamsEndpoint
{
    public sealed record TeamSummary(Guid Id, Guid ClubId, string Name, string Gender);

    public static IEndpointRouteBuilder MapListTeamsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(Guid clubId, ITeamRepository teams, CancellationToken ct)
    {
        var rows = await teams.ListByClubAsync(clubId, ct);
        return Results.Ok(rows.Select(t => new TeamSummary(t.Id, t.ClubId, t.Name, t.Gender.ToString())).ToArray());
    }
}
