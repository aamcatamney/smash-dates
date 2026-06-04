using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using smash_dates.Models;

namespace smash_dates.Services.Notifications;

// Delivers outbox notifications over SMTP via MailKit. Registered in place of the logging
// sender when an Smtp:Host is configured (see NotificationSenderSetup).
public sealed class SmtpNotificationSender : INotificationSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpNotificationSender> _logger;

    public SmtpNotificationSender(IOptions<SmtpOptions> options, ILogger<SmtpNotificationSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(Notification notification, CancellationToken ct = default)
    {
        // This sender is only registered when a host is configured (see NotificationSenderSetup);
        // guard anyway so a misconfiguration fails legibly rather than as a null-ref deep in MailKit.
        var host = _options.Host;
        if (string.IsNullOrEmpty(host))
            throw new InvalidOperationException("SMTP host is not configured.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(notification.RecipientEmail));
        message.Subject = notification.Subject;
        message.Body = new TextPart("plain") { Text = notification.Body };

        // StartTlsWhenAvailable upgrades to TLS if the server offers it but still works against a
        // plaintext relay (e.g. a local test server) — UseStartTls=false forces no upgrade.
        var secureSocket = _options.UseStartTls
            ? SecureSocketOptions.StartTlsWhenAvailable
            : SecureSocketOptions.None;

        using var client = new SmtpClient();
        await client.ConnectAsync(host, _options.Port, secureSocket, ct);
        if (!string.IsNullOrEmpty(_options.Username))
        {
            await client.AuthenticateAsync(_options.Username, _options.Password ?? string.Empty, ct);
        }
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(quit: true, ct);

        _logger.LogInformation(
            "Sent notification {Id} to {Recipient} via SMTP.", notification.Id, notification.RecipientEmail);
    }
}
