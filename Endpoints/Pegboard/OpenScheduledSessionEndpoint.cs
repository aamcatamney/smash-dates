using System.Security.Claims;
using Npgsql;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Pegboard;

// Open a previously Scheduled session: Scheduled -> Open, making it the club's live board.
// Enforces the one-Open-per-club rule (409 if another session is already live).
public static class OpenScheduledSessionEndpoint
{
    public sealed record SessionDto(Guid Id, Guid ClubId, string Name, string Status);

    public static IEndpointRouteBuilder MapOpenScheduledSessionEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{sessionId:guid}/open", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId, Guid sessionId, ClaimsPrincipal principal,
        IClubAdminRepository admins, ISessionHostRepository hosts,
        IPegboardRepository pegboard, CancellationToken ct)
    {
        var session = await pegboard.GetSessionAsync(sessionId, ct);
        if (session is null || session.ClubId != clubId) return Results.NotFound();

        var authz = await SessionAuthorizer.RequireSessionRunnerAsync(principal, clubId, admins, hosts, ct);
        if (authz is not null) return authz;

        if (session.Status != Models.PegboardSessionStatus.Scheduled)
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Session is not scheduled");

        try
        {
            var opened = await pegboard.OpenScheduledAsync(sessionId, principal.UserId()!.Value, ct);
            // No rows means the session left Scheduled between the read and the write.
            if (!opened)
                return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Session is not scheduled");
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            // The club already has an Open session — the partial unique index blocked the transition.
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "This club already has an open session");
        }

        return Results.Ok(new SessionDto(sessionId, clubId, session.Name, "Open"));
    }
}
