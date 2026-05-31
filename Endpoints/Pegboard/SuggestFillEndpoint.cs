using System.Security.Claims;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;
using smash_dates.Services.Pegboard;

namespace smash_dates.Endpoints.Pegboard;

public static class SuggestFillEndpoint
{
    public sealed record SuggestRequest(string Type);
    public sealed record SuggestResponse(List<Guid> SideA, List<Guid> SideB);

    public static IEndpointRouteBuilder MapSuggestFillEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{sessionId:guid}/suggest", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId, Guid sessionId, SuggestRequest request, ClaimsPrincipal principal,
        IPegboardRepository pegboard, IClubAdminRepository admins, ISessionHostRepository hosts, CancellationToken ct)
    {
        var (_, error) = await PegboardGuards.LoadOpenForMutationAsync(clubId, sessionId, principal, pegboard, admins, hosts, ct);
        if (error is not null) return error;

        if (!Enum.TryParse<GameType>(request.Type, out var type))
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid game type");

        var waiting = await pegboard.ListWaitingAsync(sessionId, ct);
        var pool = waiting.Select((w, i) => new FillCandidate(w.Id, w.Gender, w.Grade, i)).ToList();
        var pairs = await pegboard.ListPlayedPairsAsync(sessionId, ct);

        var suggestion = PegboardFiller.Suggest(type, pool, pairs);
        return suggestion is null
            ? Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Not enough waiting players to form this game")
            : Results.Ok(new SuggestResponse(suggestion.SideA.ToList(), suggestion.SideB.ToList()));
    }
}
