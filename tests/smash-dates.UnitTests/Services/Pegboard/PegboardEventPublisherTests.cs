using smash_dates.Services.Pegboard;

namespace smash_dates.UnitTests.Services.Pegboard;

public class PegboardEventPublisherTests
{
    [Fact]
    public async Task Subscriber_ReceivesSignal_ForItsSession()
    {
        var pub = new PegboardEventPublisher();
        var sessionId = Guid.NewGuid();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var reader = pub.Subscribe(sessionId);
        pub.Publish(sessionId);

        var got = await reader.WaitToReadAsync(cts.Token);
        got.Should().BeTrue();
    }

    [Fact]
    public async Task Subscriber_DoesNotReceive_OtherSessionsSignal()
    {
        var pub = new PegboardEventPublisher();
        var mine = Guid.NewGuid();
        var other = Guid.NewGuid();
        var reader = pub.Subscribe(mine);

        pub.Publish(other);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var act = async () => await reader.WaitToReadAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
