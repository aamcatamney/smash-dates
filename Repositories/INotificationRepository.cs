using smash_dates.Models;

namespace smash_dates.Repositories;

public interface INotificationRepository
{
    Task EnqueueAsync(string recipientEmail, string subject, string body, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> ListRecentAsync(int limit = 200, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> ListUnsentAsync(int limit = 100, CancellationToken ct = default);
    Task MarkSentAsync(Guid id, CancellationToken ct = default);
}
