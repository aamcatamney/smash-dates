using System.Net;
using System.Net.Http.Json;
using smash_dates.Models;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class RegistrationTransferEndpointsTests : IntegrationTestBase
{
    public RegistrationTransferEndpointsTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record PlayerDto(Guid Id, string FullName, string Gender);
    private sealed record CreatedDto(Guid Id);
    private sealed record RegistrationDto(Guid Id, Guid PlayerId, Guid ClubId, string Discipline, string Status);
    private sealed record PlayerLinkDto(Guid PlayerId, string FullName, string Gender, string Type);
    private sealed record TransferDto(Guid Id, Guid PlayerId, string Discipline, Guid FromClubId, Guid ToClubId, string Status, bool ReleasingApproved, bool LeagueApproved);
    private sealed record TransferCandidateDto(Guid PlayerId, string FullName, string Gender, Guid LeagueId, string LeagueName, string Discipline, string CurrentClubShortCode);
    private sealed record Setup(Guid LeagueId, Guid ClubA, Guid ClubB, Guid PlayerId);

    // Player confirmed for Level at ClubA; ClubB is an accepted member ready to receive.
    private async Task<Setup> ArrangeConfirmedAtAAsync()
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
        var reg = await (await Client.PostAsJsonAsync($"/api/clubs/{clubA}/players/{player!.Id}/registrations",
            new { leagueId, discipline = "Level" })).Content.ReadFromJsonAsync<CreatedDto>();
        await Client.PostAsync($"/api/leagues/{leagueId}/registrations/{reg!.Id}/confirm", null);
        return new Setup(leagueId, clubA, clubB, player.Id);
    }

    private Task<HttpResponseMessage> OpenTransfer(Setup s) =>
        Client.PostAsJsonAsync($"/api/clubs/{s.ClubB}/transfers", new { playerId = s.PlayerId, leagueId = s.LeagueId, discipline = "Level" });

    [Fact]
    public async Task Candidates_FindConfirmedRegistrationInSharedLeague()
    {
        var s = await ArrangeConfirmedAtAAsync();

        var rows = await Client.GetFromJsonAsync<TransferCandidateDto[]>(
            $"/api/clubs/{s.ClubB}/transfers/candidates?search=jane");

        rows!.Should().ContainSingle(r =>
            r.PlayerId == s.PlayerId && r.Discipline == "Level" &&
            r.LeagueId == s.LeagueId && r.CurrentClubShortCode == "ACME");
    }

    [Fact]
    public async Task Candidates_ExcludeLeaguesTheClubIsNotIn()
    {
        var s = await ArrangeConfirmedAtAAsync();
        // A club that shares no league with the player's confirmed registration.
        var clubC = await Seeder.CreateClubAsync("Gamma", "GAMA", contactEmail: "c@test");

        var rows = await Client.GetFromJsonAsync<TransferCandidateDto[]>(
            $"/api/clubs/{clubC}/transfers/candidates?search=jane");

        rows!.Should().BeEmpty();
    }

    [Fact]
    public async Task Transfer_BothApprovals_MovesRegistrationAndGrantsMembership()
    {
        var s = await ArrangeConfirmedAtAAsync();
        var transfer = await (await OpenTransfer(s)).Content.ReadFromJsonAsync<CreatedDto>();

        var release = await Client.PostAsync($"/api/clubs/{s.ClubA}/transfers/{transfer!.Id}/approve", null);
        var league = await Client.PostAsync($"/api/leagues/{s.LeagueId}/transfers/{transfer.Id}/approve", null);

        release.StatusCode.Should().Be(HttpStatusCode.NoContent);
        league.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Registration now sits at ClubB, Confirmed.
        var bRegs = await Client.GetFromJsonAsync<RegistrationDto[]>($"/api/clubs/{s.ClubB}/registrations");
        bRegs!.Should().ContainSingle(r => r.PlayerId == s.PlayerId && r.ClubId == s.ClubB && r.Status == "Confirmed");
        // ClubA no longer holds it.
        var aRegs = await Client.GetFromJsonAsync<RegistrationDto[]>($"/api/clubs/{s.ClubA}/registrations");
        aRegs!.Should().NotContain(r => r.ClubId == s.ClubA && r.Status == "Confirmed");
        // ClubB gained a Member affiliation.
        var bPlayers = await Client.GetFromJsonAsync<PlayerLinkDto[]>($"/api/clubs/{s.ClubB}/players");
        bPlayers!.Should().ContainSingle(l => l.PlayerId == s.PlayerId && l.Type == "Member");
    }

    [Fact]
    public async Task Transfer_PendingUntilBothApprove()
    {
        var s = await ArrangeConfirmedAtAAsync();
        var transfer = await (await OpenTransfer(s)).Content.ReadFromJsonAsync<CreatedDto>();

        await Client.PostAsync($"/api/clubs/{s.ClubA}/transfers/{transfer!.Id}/approve", null); // releasing only

        var transfers = await Client.GetFromJsonAsync<TransferDto[]>($"/api/leagues/{s.LeagueId}/transfers");
        var t = transfers!.Single();
        t.Status.Should().Be("Pending");
        t.ReleasingApproved.Should().BeTrue();
        t.LeagueApproved.Should().BeFalse();
    }

    [Fact]
    public async Task Transfer_LeagueRejects_RegistrationStaysAtReleasingClub()
    {
        var s = await ArrangeConfirmedAtAAsync();
        var transfer = await (await OpenTransfer(s)).Content.ReadFromJsonAsync<CreatedDto>();

        var reject = await Client.PostAsync($"/api/leagues/{s.LeagueId}/transfers/{transfer!.Id}/reject", null);

        reject.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var aRegs = await Client.GetFromJsonAsync<RegistrationDto[]>($"/api/clubs/{s.ClubA}/registrations");
        aRegs!.Should().ContainSingle(r => r.ClubId == s.ClubA && r.Status == "Confirmed");
    }

    [Fact]
    public async Task Transfer_NoConfirmedRegistration_Returns409()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        var clubB = await Seeder.CreateClubAsync("Beta", "BETA", contactEmail: "b@test");
        await Seeder.CreateMembershipAsync(clubB, leagueId, MembershipStatus.Accepted);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });
        var player = await (await Client.PostAsJsonAsync($"/api/clubs/{clubB}/players",
            new { fullName = "Jane Smith", gender = "Female", type = "Member" })).Content.ReadFromJsonAsync<PlayerDto>();

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubB}/transfers",
            new { playerId = player!.Id, leagueId, discipline = "Level" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
