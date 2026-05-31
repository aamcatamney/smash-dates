using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using smash_dates.Services.Notifications;

namespace smash_dates.IntegrationTests.Services;

// Sender selection is config-driven: SMTP when a host is set, logging otherwise.
public sealed class NotificationSenderSetupTests
{
    private static ServiceDescriptor SenderDescriptor(params (string Key, string? Value)[] settings)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build();

        var services = new ServiceCollection();
        services.AddNotificationSender(config);
        return services.Single(d => d.ServiceType == typeof(INotificationSender));
    }

    [Fact]
    public void UsesSmtpSender_WhenHostConfigured()
    {
        SenderDescriptor(("Smtp:Host", "mail.example.com"))
            .ImplementationType.Should().Be<SmtpNotificationSender>();
    }

    [Fact]
    public void UsesLoggingSender_WhenHostMissing()
    {
        SenderDescriptor().ImplementationType.Should().Be<LoggingNotificationSender>();
    }

    [Fact]
    public void UsesLoggingSender_WhenHostBlank()
    {
        SenderDescriptor(("Smtp:Host", "   "))
            .ImplementationType.Should().Be<LoggingNotificationSender>();
    }
}
