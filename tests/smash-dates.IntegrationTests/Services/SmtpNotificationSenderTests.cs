using System.Net.Http;
using System.Net.Http.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using smash_dates.Models;
using smash_dates.Services;
using smash_dates.Services.Notifications;

namespace smash_dates.IntegrationTests.Services;

// End-to-end SMTP delivery against a throwaway Mailpit server, asserting the message
// actually lands (via Mailpit's HTTP API) rather than just that the call didn't throw.
public sealed class SmtpNotificationSenderTests : IAsyncLifetime
{
    private readonly IContainer _mailpit = new ContainerBuilder()
        .WithImage("axllent/mailpit:latest")
        .WithPortBinding(1025, true) // SMTP
        .WithPortBinding(8025, true) // HTTP API + UI
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8025).ForPath("/")))
        .Build();

    public async ValueTask InitializeAsync() => await _mailpit.StartAsync();

    public async ValueTask DisposeAsync() => await _mailpit.DisposeAsync();

    [Fact]
    public async Task SendAsync_DeliversTheMessageToTheSmtpServer()
    {
        var options = Options.Create(new SmtpOptions
        {
            Host = _mailpit.Hostname,
            Port = _mailpit.GetMappedPublicPort(1025),
            UseStartTls = false, // Mailpit speaks plaintext SMTP by default.
            FromAddress = "no-reply@smash-dates.test",
            FromName = "Smash Dates",
        });
        var sender = new SmtpNotificationSender(options, new StubVersion("v2026.6.0-test"), NullLogger<SmtpNotificationSender>.Instance);

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            RecipientEmail = "player@example.com",
            Subject = "Verify your smash-dates email",
            Body = "Confirm your email to finish setting up your account.",
        };

        await sender.SendAsync(notification);

        using var http = new HttpClient
        {
            BaseAddress = new Uri($"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(8025)}"),
        };
        var inbox = await http.GetFromJsonAsync<MailpitMessages>("/api/v1/messages");

        inbox.Should().NotBeNull();
        inbox!.Messages.Should().ContainSingle();
        inbox.Messages[0].Subject.Should().Be("Verify your smash-dates email");
        inbox.Messages[0].To.Should().ContainSingle(t => t.Address == "player@example.com");

        // The delivered body carries the build version in a footer, sourced from IAppVersion.
        var delivered = await http.GetFromJsonAsync<MailpitMessage>($"/api/v1/message/{inbox.Messages[0].ID}");
        delivered!.Text.Should().Contain("Confirm your email to finish setting up your account.");
        delivered.Text.Should().Contain("smash-dates v2026.6.0-test");
    }

    private sealed record MailpitMessages(List<MailpitMessage> Messages);
    private sealed record MailpitMessage(string ID, string Subject, string Text, List<MailpitAddress> To);
    private sealed record MailpitAddress(string Address);

    private sealed class StubVersion(string current) : IAppVersion
    {
        public string Current { get; } = current;
    }
}
