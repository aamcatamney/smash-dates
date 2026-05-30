using smash_dates.Models;

namespace smash_dates.Repositories;

public interface INotificationRepository
{
    Task EnqueueAsync(string recipientEmail, string subject, string body, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> ListRecentAsync(int limit = 200, CancellationToken ct = default);
}
