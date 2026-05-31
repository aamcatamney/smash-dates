using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace smash_dates.Services.Notifications;

public static class NotificationSenderSetup
{
    // Picks the notification sender from config: a real SMTP sender when Smtp:Host is set,
    // otherwise the logging sender. Keeps SMTP opt-in so dev/tests need no mail server.
    public static IServiceCollection AddNotificationSender(this IServiceCollection services, IConfiguration config)
    {
        var smtp = config.GetSection(SmtpOptions.SectionName);

        if (!string.IsNullOrWhiteSpace(smtp[nameof(SmtpOptions.Host)]))
        {
            services.Configure<SmtpOptions>(smtp);
            services.AddSingleton<INotificationSender, SmtpNotificationSender>();
        }
        else
        {
            services.AddSingleton<INotificationSender, LoggingNotificationSender>();
        }

        return services;
    }
}
