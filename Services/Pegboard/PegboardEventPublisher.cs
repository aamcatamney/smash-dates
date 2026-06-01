using System.Collections.Concurrent;
using System.Threading.Channels;

namespace smash_dates.Services.Pegboard;

/// In-process fan-out of content-free "board changed" signals, keyed by session.
/// Single-instance only (see docs/adr/0004). Each subscriber gets a bounded channel
/// that drops to the latest signal — a missed tick self-heals on the next read.
public sealed class PegboardEventPublisher : IPegboardEventPublisher
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Channel<byte>, byte>> _subs = new();

    public ChannelReader<byte> Subscribe(Guid sessionId)
    {
        var channel = Channel.CreateBounded<byte>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });
        var set = _subs.GetOrAdd(sessionId, _ => new ConcurrentDictionary<Channel<byte>, byte>());
        set[channel] = 0;
        return channel.Reader;
    }

    public void Unsubscribe(Guid sessionId, ChannelReader<byte> reader)
    {
        if (!_subs.TryGetValue(sessionId, out var set)) return;
        foreach (var ch in set.Keys)
        {
            if (ReferenceEquals(ch.Reader, reader))
            {
                set.TryRemove(ch, out _);
                ch.Writer.TryComplete();
                break;
            }
        }
        if (set.IsEmpty) _subs.TryRemove(sessionId, out _);
    }

    public void Publish(Guid sessionId)
    {
        if (!_subs.TryGetValue(sessionId, out var set)) return;
        foreach (var ch in set.Keys) ch.Writer.TryWrite(0);
    }
}
