using Microsoft.Extensions.Logging;
using smash_dates.Models;

namespace smash_dates.Services.Notifications;

// Delivers a notification to its recipient. The default implementation logs only; a real
// SMTP/provider implementation is a config-swap (no provider/secret in this environment).
public interface INotificationSender
{
    Task SendAsync(Notification notification, CancellationToken ct = default);
}

public sealed class LoggingNotificationSender : INotificationSender
{
    private readonly ILogger<LoggingNotificationSender> _logger;

    public LoggingNotificationSender(ILogger<LoggingNotificationSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(Notification notification, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Notification {Id} to {Recipient}: {Subject}",
            notification.Id, notification.RecipientEmail, notification.Subject);
        return Task.CompletedTask;
    }
}
