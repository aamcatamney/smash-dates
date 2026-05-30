using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using smash_dates.Services.Notifications;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class NotificationDrainerTests : IntegrationTestBase
{
    public NotificationDrainerTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record NotificationDto(Guid Id, string RecipientEmail, string Subject, string? SentAt);

    [Fact]
    public async Task Drain_MarksUnsentNotificationsAsSent()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME", contactEmail: "acme@club.test");
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });
        await Client.PostAsJsonAsync($"/api/leagues/{leagueId}/memberships", new { clubId });

        // Before draining: the enqueued notification is unsent.
        var before = await Client.GetFromJsonAsync<NotificationDto[]>("/api/notifications");
        before!.Single(n => n.RecipientEmail == "acme@club.test").SentAt.Should().BeNull();

        using (var scope = Factory.Services.CreateScope())
        {
            var drainer = scope.ServiceProvider.GetRequiredService<NotificationDrainer>();
            await drainer.DrainAsync(default);
        }

        var after = await Client.GetFromJsonAsync<NotificationDto[]>("/api/notifications");
        after!.Single(n => n.RecipientEmail == "acme@club.test").SentAt.Should().NotBeNull();
    }
}
