using System.Security.Claims;
using smash_dates.Repositories;
using smash_dates.Services.Pegboard;

namespace smash_dates.Endpoints.Pegboard;

public static class CloseSessionEndpoint
{
    public static IEndpointRouteBuilder MapCloseSessionEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{sessionId:guid}/close", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId, Guid sessionId, ClaimsPrincipal principal,
        IPegboardRepository pegboard, IClubAdminRepository admins, ISessionHostRepository hosts,
        IPegboardEventPublisher events, CancellationToken ct)
    {
        var (_, error) = await PegboardGuards.LoadOpenForMutationAsync(clubId, sessionId, principal, pegboard, admins, hosts, ct);
        if (error is not null) return error;

        await pegboard.CloseAsync(sessionId, ct);
        events.Publish(sessionId);
        return Results.NoContent();
    }
}
