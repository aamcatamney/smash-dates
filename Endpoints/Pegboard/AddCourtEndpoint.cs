using System.Security.Claims;
using smash_dates.Repositories;
using smash_dates.Services.Pegboard;

namespace smash_dates.Endpoints.Pegboard;

public static class AddCourtEndpoint
{
    private const int MaxLabel = 50;
    public sealed record AddCourtRequest(string Label);
    public sealed record CourtDto(Guid Id, string Label);

    public static IEndpointRouteBuilder MapAddCourtEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{sessionId:guid}/courts", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId, Guid sessionId, AddCourtRequest request, ClaimsPrincipal principal,
        IPegboardRepository pegboard, IClubAdminRepository admins, ISessionHostRepository hosts,
        IPegboardEventPublisher events, CancellationToken ct)
    {
        var (_, error) = await PegboardGuards.LoadOpenForMutationAsync(clubId, sessionId, principal, pegboard, admins, hosts, ct);
        if (error is not null) return error;

        var label = (request.Label ?? string.Empty).Trim();
        if (label.Length == 0 || label.Length > MaxLabel)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid label");

        var id = await pegboard.AddCourtAsync(sessionId, label, ct);
        events.Publish(sessionId);
        return Results.Created($"/api/clubs/{clubId}/pegboard/sessions/{sessionId}/courts/{id}", new CourtDto(id, label));
    }
}
