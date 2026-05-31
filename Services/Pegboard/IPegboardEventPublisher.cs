using System.Threading.Channels;

namespace smash_dates.Services.Pegboard;

public interface IPegboardEventPublisher
{
    // Subscribe to a session's board-changed signals. Dispose-free: the reader completes
    // when the caller stops reading (the SSE request ends).
    ChannelReader<byte> Subscribe(Guid sessionId);
    void Unsubscribe(Guid sessionId, ChannelReader<byte> reader);
    void Publish(Guid sessionId);
}
