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
    private readonly IAppVersion _version;
    private readonly ILogger<LoggingNotificationSender> _logger;

    public LoggingNotificationSender(IAppVersion version, ILogger<LoggingNotificationSender> logger)
    {
        _version = version;
        _logger = logger;
    }

    public Task SendAsync(Notification notification, CancellationToken ct = default)
    {
        // Mirror the real sender's version stamp so the dev/test log matches what SMTP would deliver.
        _logger.LogInformation(
            "Notification {Id} to {Recipient}: {Subject} [smash-dates {Version}]",
            notification.Id, notification.RecipientEmail, notification.Subject, _version.Current);
        return Task.CompletedTask;
    }
}
