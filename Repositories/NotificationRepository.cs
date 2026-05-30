using Dapper;
using smash_dates.Data;
using smash_dates.Models;

namespace smash_dates.Repositories;

public sealed class NotificationRepository : INotificationRepository
{
    private readonly IDbConnectionFactory _factory;

    public NotificationRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task EnqueueAsync(string recipientEmail, string subject, string body, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync(
            new CommandDefinition(
                @"INSERT INTO notifications (recipient_email, subject, body)
                  VALUES (@recipientEmail, @subject, @body)",
                new { recipientEmail, subject, body },
                cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Notification>> ListRecentAsync(int limit = 200, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<Notification>(
            new CommandDefinition(
                @"SELECT id, recipient_email, subject, body, created_at, sent_at
                  FROM notifications
                  ORDER BY created_at DESC
                  LIMIT @limit",
                new { limit },
                cancellationToken: ct));
        return rows.AsList();
    }
}
