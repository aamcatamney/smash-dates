using smash_dates.Repositories;

namespace smash_dates.Endpoints.Pegboard;

// Closed-session player breakdown: each attendee's matches and court-vs-waiting time split.
// A read, available to any authenticated user (like the board read).
public static class SessionSummaryEndpoint
{
    public static IEndpointRouteBuilder MapSessionSummaryEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{sessionId:guid}/summary", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId, Guid sessionId, IPegboardRepository pegboard, CancellationToken ct)
    {
        var session = await pegboard.GetSessionAsync(sessionId, ct);
        if (session is null || session.ClubId != clubId) return Results.NotFound();

        var summary = await pegboard.GetSessionSummaryAsync(sessionId, ct);
        return summary is null ? Results.NotFound() : Results.Ok(summary);
    }
}
