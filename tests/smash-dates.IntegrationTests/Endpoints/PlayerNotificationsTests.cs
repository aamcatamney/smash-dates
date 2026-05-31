using System.Net.Http.Json;
using smash_dates.Models;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

// Player registration / transfer events should enqueue outbox notifications, like every
// other domain event in the app.
public sealed class PlayerNotificationsTests : IntegrationTestBase
{
    public PlayerNotificationsTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record PlayerDto(Guid Id, string FullName, string Gender);
    private sealed record CreatedDto(Guid Id);
    private sealed record NotificationDto(Guid Id, string RecipientEmail, string Subject, string? SentAt);

    private Task<NotificationDto[]?> Notifications() =>
        Client.GetFromJsonAsync<NotificationDto[]>("/api/notifications");

    [Fact]
    public async Task RegistrationRequested_NotifiesLeagueAdmin()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("Riverside League", admin.Id);
        var la = await Seeder.CreateUserAsync("la@example.com", "correct-horse-battery");
        await Seeder.GrantLeagueAdminAsync(leagueId, la.Id);
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME", contactEmail: "acme@club.test");
        await Seeder.CreateMembershipAsync(clubId, leagueId, MembershipStatus.Accepted);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });
        var player = await (await Client.PostAsJsonAsync($"/api/clubs/{clubId}/players",
            new { fullName = "Jane Smith", gender = "Female", type = "Member" })).Content.ReadFromJsonAsync<PlayerDto>();

        await Client.PostAsJsonAsync($"/api/clubs/{clubId}/players/{player!.Id}/registrations",
            new { leagueId, discipline = "Level" });

        var notes = await Notifications();
        notes!.Should().Contain(n => n.RecipientEmail == "la@example.com" && n.Subject.Contains("egistration"));
    }

    [Fact]
    public async Task TransferCompleted_NotifiesBothClubs()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("Riverside League", admin.Id);
        var clubA = await Seeder.CreateClubAsync("Acme", "ACME", contactEmail: "acme@club.test");
        var clubB = await Seeder.CreateClubAsync("Beta", "BETA", contactEmail: "beta@club.test");
        await Seeder.CreateMembershipAsync(clubA, leagueId, MembershipStatus.Accepted);
        await Seeder.CreateMembershipAsync(clubB, leagueId, MembershipStatus.Accepted);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });
        var player = await (await Client.PostAsJsonAsync($"/api/clubs/{clubA}/players",
            new { fullName = "Jane Smith", gender = "Female", type = "Member" })).Content.ReadFromJsonAsync<PlayerDto>();
        var reg = await (await Client.PostAsJsonAsync($"/api/clubs/{clubA}/players/{player!.Id}/registrations",
            new { leagueId, discipline = "Level" })).Content.ReadFromJsonAsync<CreatedDto>();
        await Client.PostAsync($"/api/leagues/{leagueId}/registrations/{reg!.Id}/confirm", null);
        var transfer = await (await Client.PostAsJsonAsync($"/api/clubs/{clubB}/transfers",
            new { playerId = player.Id, leagueId, discipline = "Level" })).Content.ReadFromJsonAsync<CreatedDto>();
        await Client.PostAsync($"/api/clubs/{clubA}/transfers/{transfer!.Id}/approve", null);
        await Client.PostAsync($"/api/leagues/{leagueId}/transfers/{transfer.Id}/approve", null);

        var notes = await Notifications();
        notes!.Should().Contain(n => n.RecipientEmail == "acme@club.test" && n.Subject.Contains("ransfer"));
        notes!.Should().Contain(n => n.RecipientEmail == "beta@club.test" && n.Subject.Contains("ransfer"));
    }
}
