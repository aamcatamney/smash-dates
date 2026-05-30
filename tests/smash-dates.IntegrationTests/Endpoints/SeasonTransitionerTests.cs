using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using smash_dates.Models;
using smash_dates.Services.Seasons;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class SeasonTransitionerTests : IntegrationTestBase
{
    public SeasonTransitionerTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record SeasonDto(Guid Id, string Status);

    private async Task<(Guid leagueId, Guid seasonId)> ProposedSeasonWithMatchOn(DateOnly matchDate)
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        var seasonId = await Seeder.CreateSeasonAsync(leagueId, "2025/26", new DateOnly(2025, 9, 1), new DateOnly(2025, 9, 30), SeasonStatus.Proposed);
        var divisionId = await Seeder.CreateDivisionAsync(leagueId, "Mens 1", DivisionGender.Mens, 1, 9);
        var clubA = await Seeder.CreateClubAsync("Acme", "ACME");
        var clubB = await Seeder.CreateClubAsync("Beta", "BETA");
        var venueA = await Seeder.CreateVenueAsync(clubA, "Acme Hall", 1);
        var teamA = await Seeder.CreateTeamAsync(clubA, "Acme 1", DivisionGender.Mens);
        var teamB = await Seeder.CreateTeamAsync(clubB, "Beta 1", DivisionGender.Mens);
        await Seeder.CreateMatchAsync(seasonId, divisionId, teamA, teamB, venueA, matchDate);
        return (leagueId, seasonId);
    }

    private async Task Run(DateOnly today)
    {
        using var scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SeasonTransitioner>().RunAsync(today, default);
    }

    private async Task<string> Status(Guid leagueId, Guid seasonId)
    {
        await Client.LoginAsAsync("reader@example.com", "correct-horse-battery", Seeder);
        var s = await Client.GetFromJsonAsync<SeasonDto>($"/api/leagues/{leagueId}/seasons/{seasonId}");
        return s!.Status;
    }

    [Fact]
    public async Task ProposedSeason_OnceFirstMatchDateReached_BecomesActive()
    {
        var (leagueId, seasonId) = await ProposedSeasonWithMatchOn(new DateOnly(2025, 9, 1));

        await Run(new DateOnly(2025, 9, 2));

        (await Status(leagueId, seasonId)).Should().Be("Active");
    }

    [Fact]
    public async Task ProposedSeason_BeforeFirstMatchDate_StaysProposed()
    {
        var (leagueId, seasonId) = await ProposedSeasonWithMatchOn(new DateOnly(2025, 9, 20));

        await Run(new DateOnly(2025, 9, 1));

        (await Status(leagueId, seasonId)).Should().Be("Proposed");
    }

    [Fact]
    public async Task ActiveSeason_PastEndDate_BecomesClosed()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        var seasonId = await Seeder.CreateSeasonAsync(leagueId, "2025/26", new DateOnly(2025, 9, 1), new DateOnly(2025, 9, 30), SeasonStatus.Active);

        await Run(new DateOnly(2025, 10, 1));

        (await Status(leagueId, seasonId)).Should().Be("Closed");
    }
}
