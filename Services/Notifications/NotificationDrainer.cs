using smash_dates.Repositories;

namespace smash_dates.Services.Notifications;

// Delivers any unsent outbox notifications and stamps them sent. Idempotent: MarkSent only
// affects rows still unsent, so concurrent drains are safe.
public sealed class NotificationDrainer
{
    private readonly INotificationRepository _outbox;
    private readonly INotificationSender _sender;

    public NotificationDrainer(INotificationRepository outbox, INotificationSender sender)
    {
        _outbox = outbox;
        _sender = sender;
    }

    public async Task DrainAsync(CancellationToken ct)
    {
        foreach (var notification in await _outbox.ListUnsentAsync(ct: ct))
        {
            await _sender.SendAsync(notification, ct);
            await _outbox.MarkSentAsync(notification.Id, ct);
        }
    }
}
