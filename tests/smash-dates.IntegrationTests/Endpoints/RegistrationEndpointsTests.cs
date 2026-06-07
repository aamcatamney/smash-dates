using System.Net;
using System.Net.Http.Json;
using smash_dates.Models;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class RegistrationEndpointsTests : IntegrationTestBase
{
    public RegistrationEndpointsTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record PlayerDto(Guid Id, string FullName, string Gender);
    private sealed record CreatedDto(Guid Id);
    private sealed record RegistrationDto(Guid Id, Guid PlayerId, Guid ClubId, string Discipline, string Status);
    private sealed record Setup(Guid LeagueId, Guid ClubA, Guid ClubB, Guid PlayerId);

    private async Task<Setup> ArrangeAsync()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        var clubA = await Seeder.CreateClubAsync("Acme", "ACME", contactEmail: "a@test");
        var clubB = await Seeder.CreateClubAsync("Beta", "BETA", contactEmail: "b@test");
        await Seeder.CreateMembershipAsync(clubA, leagueId, MembershipStatus.Accepted);
        await Seeder.CreateMembershipAsync(clubB, leagueId, MembershipStatus.Accepted);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });
        var player = await (await Client.PostAsJsonAsync($"/api/clubs/{clubA}/players",
            new { fullName = "Jane Smith", gender = "Female", type = "Member" })).Content.ReadFromJsonAsync<PlayerDto>();
        return new Setup(leagueId, clubA, clubB, player!.Id);
    }

    private Task<HttpResponseMessage> Request(Guid clubId, Guid playerId, Guid leagueId, string discipline) =>
        Client.PostAsJsonAsync($"/api/clubs/{clubId}/players/{playerId}/registrations", new { leagueId, discipline });

    [Fact]
    public async Task Request_AsMember_CreatesPending()
    {
        var s = await ArrangeAsync();

        var response = await Request(s.ClubA, s.PlayerId, s.LeagueId, "Level");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var regs = await Client.GetFromJsonAsync<RegistrationDto[]>($"/api/leagues/{s.LeagueId}/registrations");
        regs!.Should().ContainSingle(r => r.PlayerId == s.PlayerId && r.Status == "Pending" && r.Discipline == "Level");
    }

    [Fact]
    public async Task Confirm_PromotesToConfirmed()
    {
        var s = await ArrangeAsync();
        var reg = await (await Request(s.ClubA, s.PlayerId, s.LeagueId, "Level")).Content.ReadFromJsonAsync<CreatedDto>();

        var response = await Client.PostAsync($"/api/leagues/{s.LeagueId}/registrations/{reg!.Id}/confirm", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var regs = await Client.GetFromJsonAsync<RegistrationDto[]>($"/api/leagues/{s.LeagueId}/registrations");
        regs!.Single().Status.Should().Be("Confirmed");
    }

    [Fact]
    public async Task Request_ForVisitor_Returns409()
    {
        var s = await ArrangeAsync();
        await Client.PatchAsJsonAsync($"/api/clubs/{s.ClubA}/players/{s.PlayerId}", new { type = "Visitor" });

        var response = await Request(s.ClubA, s.PlayerId, s.LeagueId, "Level");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Request_WhenAlreadyConfirmedAtAnotherClub_Returns409()
    {
        var s = await ArrangeAsync();
        var reg = await (await Request(s.ClubA, s.PlayerId, s.LeagueId, "Level")).Content.ReadFromJsonAsync<CreatedDto>();
        await Client.PostAsync($"/api/leagues/{s.LeagueId}/registrations/{reg!.Id}/confirm", null);
        // Same player held as a Member of a second club — a state reached in product via a transfer.
        await Seeder.LinkPlayerToClubAsync(s.PlayerId, s.ClubB);

        var response = await Request(s.ClubB, s.PlayerId, s.LeagueId, "Level");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Confirm_SecondClubForSameDiscipline_Returns409_ExclusivityEnforced()
    {
        var s = await ArrangeAsync();
        // Same player held as a Member of a second club — a state reached in product via a transfer.
        await Seeder.LinkPlayerToClubAsync(s.PlayerId, s.ClubB);
        var regA = await (await Request(s.ClubA, s.PlayerId, s.LeagueId, "Level")).Content.ReadFromJsonAsync<CreatedDto>();
        var regB = await (await Request(s.ClubB, s.PlayerId, s.LeagueId, "Level")).Content.ReadFromJsonAsync<CreatedDto>();
        await Client.PostAsync($"/api/leagues/{s.LeagueId}/registrations/{regA!.Id}/confirm", null);

        var response = await Client.PostAsync($"/api/leagues/{s.LeagueId}/registrations/{regB!.Id}/confirm", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Reject_SetsRejected()
    {
        var s = await ArrangeAsync();
        var reg = await (await Request(s.ClubA, s.PlayerId, s.LeagueId, "Mixed")).Content.ReadFromJsonAsync<CreatedDto>();

        var response = await Client.PostAsync($"/api/leagues/{s.LeagueId}/registrations/{reg!.Id}/reject", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var regs = await Client.GetFromJsonAsync<RegistrationDto[]>($"/api/leagues/{s.LeagueId}/registrations");
        regs!.Single().Status.Should().Be("Rejected");
    }
}
