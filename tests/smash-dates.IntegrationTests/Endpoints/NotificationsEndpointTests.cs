using System.Net;
using System.Net.Http.Json;
using smash_dates.Models;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class NotificationsEndpointTests : IntegrationTestBase
{
    public NotificationsEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record NotificationDto(Guid Id, string RecipientEmail, string Subject, string Body, string? SentAt);

    [Fact]
    public async Task InvitingClub_EnqueuesNotificationToClubContact()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME", contactEmail: "acme@club.test");
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var invite = await Client.PostAsJsonAsync($"/api/leagues/{leagueId}/memberships", new { clubId });
        invite.StatusCode.Should().Be(HttpStatusCode.Created);

        var notifications = await Client.GetFromJsonAsync<NotificationDto[]>("/api/notifications");
        notifications.Should().Contain(n => n.RecipientEmail == "acme@club.test" && n.Subject.Contains("Invit"));
    }

    [Fact]
    public async Task ForceConfirmingMatch_EnqueuesConfirmationNotifications()
    {
        var s = await Seeder.CreateProposedMatchAsync("a@x.test", "b@x.test", "correct-horse-battery");
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys-a@x.test", password = "correct-horse-battery" });

        var force = await Client.PostAsync($"/api/matches/{s.MatchId}/force-confirm", null);
        force.StatusCode.Should().Be(HttpStatusCode.OK);

        var notifications = await Client.GetFromJsonAsync<NotificationDto[]>("/api/notifications");
        notifications!.Count(n => n.Subject.Contains("Confirmed")).Should().BeGreaterThanOrEqualTo(2); // both clubs
    }

    [Fact]
    public async Task Get_AsNonSystemAdmin_Returns403()
    {
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.GetAsync("/api/notifications");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_Unauthenticated_Returns401()
    {
        var response = await Client.GetAsync("/api/notifications");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
