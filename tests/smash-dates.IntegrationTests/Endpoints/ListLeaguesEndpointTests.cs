using System.Net;
using System.Net.Http.Json;
using smash_dates.Endpoints.Leagues;
using smash_dates.Models;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class ListLeaguesEndpointTests : IntegrationTestBase
{
    public ListLeaguesEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record PlayerDto(Guid Id);
    private sealed record CreatedDto(Guid Id);

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        var response = await Client.GetAsync("/api/leagues");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Authenticated_ReturnsLeaguesSortedByName()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("admin@example.com", "correct-horse-battery");
        await Seeder.CreateLeagueAsync("Beta", admin.Id);
        await Seeder.CreateLeagueAsync("Alpha", admin.Id);
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.GetAsync("/api/leagues");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListLeaguesEndpoint.LeagueSummary[]>();
        body!.Select(s => s.Name).Should().ContainInOrder("Alpha", "Beta");
    }

    [Fact]
    public async Task Get_IncludesPerLeagueStats()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("Stats League", admin.Id);
        await Seeder.CreateDivisionAsync(leagueId, "Mens 1", DivisionGender.Mens, 1, 9);
        await Seeder.CreateDivisionAsync(leagueId, "Ladies 1", DivisionGender.Ladies, 1, 6);
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME", contactEmail: "a@test");
        await Seeder.CreateMembershipAsync(clubId, leagueId, MembershipStatus.Accepted);
        await Seeder.CreateSeasonAsync(leagueId, "2025/26", new DateOnly(2025, 9, 1), new DateOnly(2026, 4, 30), SeasonStatus.Active);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });
        var player = await (await Client.PostAsJsonAsync($"/api/clubs/{clubId}/players",
            new { fullName = "Sam Okafor", gender = "Male", type = "Member" })).Content.ReadFromJsonAsync<PlayerDto>();
        var reg = await (await Client.PostAsJsonAsync($"/api/clubs/{clubId}/players/{player!.Id}/registrations",
            new { leagueId, discipline = "Level" })).Content.ReadFromJsonAsync<CreatedDto>();
        await Client.PostAsync($"/api/leagues/{leagueId}/registrations/{reg!.Id}/confirm", null);

        var body = await Client.GetFromJsonAsync<ListLeaguesEndpoint.LeagueSummary[]>("/api/leagues");

        var item = body!.Single(l => l.Id == leagueId);
        item.DivisionCount.Should().Be(2);
        item.PlayerCount.Should().Be(1);
        item.ActiveSeasonName.Should().Be("2025/26");
    }
}
