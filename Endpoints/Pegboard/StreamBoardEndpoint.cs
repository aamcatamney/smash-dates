using System.Text;
using smash_dates.Repositories;
using smash_dates.Services.Pegboard;

namespace smash_dates.Endpoints.Pegboard;

public static class StreamBoardEndpoint
{
    public static IEndpointRouteBuilder MapStreamBoardEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{sessionId:guid}/stream", Handle);
        return app;
    }

    private static async Task Handle(
        HttpContext http, Guid clubId, Guid sessionId,
        IPegboardRepository pegboard, IPegboardEventPublisher events, CancellationToken ct)
    {
        var session = await pegboard.GetSessionAsync(sessionId, ct);
        if (session is null || session.ClubId != clubId)
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        http.Response.Headers.ContentType = "text/event-stream";
        http.Response.Headers.CacheControl = "no-cache";
        http.Response.Headers["X-Accel-Buffering"] = "no"; // disable proxy buffering

        var reader = events.Subscribe(sessionId);
        try
        {
            // Tell the client to (re)load the board immediately on connect.
            await WriteEventAsync(http, ct);
            while (await reader.WaitToReadAsync(ct))
            {
                while (reader.TryRead(out _)) { /* coalesce */ }
                await WriteEventAsync(http, ct);
            }
        }
        catch (OperationCanceledException) { /* client disconnected */ }
        finally
        {
            events.Unsubscribe(sessionId, reader);
        }
    }

    private static async Task WriteEventAsync(HttpContext http, CancellationToken ct)
    {
        // Content-free signal — the client re-fetches /board on each event (see ADR 0004).
        await http.Response.WriteAsync("event: board-changed\ndata: 1\n\n", Encoding.UTF8, ct);
        await http.Response.Body.FlushAsync(ct);
    }
}
