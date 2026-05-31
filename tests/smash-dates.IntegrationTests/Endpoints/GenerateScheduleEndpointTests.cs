using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using smash_dates.Models;
using smash_dates.Services.Scheduling;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class GenerateScheduleEndpointTests : IntegrationTestBase
{
    public GenerateScheduleEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record Setup(Guid LeagueId, Guid SeasonId, Guid DivisionId);

    // League + Draft season with two Level weeks + a Mens division with two teams from two
    // accepted-member clubs, each club with a venue. SystemAdmin seeded but not logged in.
    private async Task<Setup> ArrangeSchedulableSeason(bool withWeeks = true, SeasonStatus status = SeasonStatus.Draft)
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        var seasonId = await Seeder.CreateSeasonAsync(leagueId, "2025/26", new DateOnly(2025, 9, 1), new DateOnly(2025, 9, 30), status);
        if (withWeeks)
        {
            await Seeder.CreateSeasonWeekAsync(seasonId, new DateOnly(2025, 9, 1), new DateOnly(2025, 9, 7), WeekType.Level);
            await Seeder.CreateSeasonWeekAsync(seasonId, new DateOnly(2025, 9, 8), new DateOnly(2025, 9, 14), WeekType.Level);
        }
        var divisionId = await Seeder.CreateDivisionAsync(leagueId, "Mens 1", DivisionGender.Mens, 1, 9);

        var clubA = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.CreateMembershipAsync(clubA, leagueId, MembershipStatus.Accepted);
        await Seeder.CreateVenueAsync(clubA, "Acme Hall", 1);
        var teamA = await Seeder.CreateTeamAsync(clubA, "Acme 1", DivisionGender.Mens);
        await Seeder.CreateSeasonEntryAsync(seasonId, divisionId, teamA);

        var clubB = await Seeder.CreateClubAsync("Beta", "BETA");
        await Seeder.CreateMembershipAsync(clubB, leagueId, MembershipStatus.Accepted);
        await Seeder.CreateVenueAsync(clubB, "Beta Hall", 1);
        var teamB = await Seeder.CreateTeamAsync(clubB, "Beta 1", DivisionGender.Mens);
        await Seeder.CreateSeasonEntryAsync(seasonId, divisionId, teamB);

        return new Setup(leagueId, seasonId, divisionId);
    }

    private Task LoginSystemAdmin() =>
        Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

    // Drive the background runner explicitly (the hosted service is disabled in tests).
    private async Task RunSchedulerAsync()
    {
        using var scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ScheduleRunner>().RunAsync(default);
    }

    private Task<SeasonStatusDto?> GetSeasonAsync(Setup s) =>
        Client.GetFromJsonAsync<SeasonStatusDto>($"/api/leagues/{s.LeagueId}/seasons/{s.SeasonId}");

    [Fact]
    public async Task Post_Feasible_Accepts_MovesToScheduling_ThenRunnerProposes()
    {
        var s = await ArrangeSchedulableSeason();
        await LoginSystemAdmin();

        var response = await Client.PostAsync($"/api/leagues/{s.LeagueId}/seasons/{s.SeasonId}/generate", null);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await GetSeasonAsync(s))!.Status.Should().Be("Scheduling");

        await RunSchedulerAsync();
        (await GetSeasonAsync(s))!.Status.Should().Be("Proposed");
    }

    [Fact]
    public async Task RunnerPersistsProposedMatches()
    {
        var s = await ArrangeSchedulableSeason();
        await LoginSystemAdmin();
        await Client.PostAsync($"/api/leagues/{s.LeagueId}/seasons/{s.SeasonId}/generate", null);

        await RunSchedulerAsync();

        var matches = await Client.GetFromJsonAsync<MatchDto[]>($"/api/leagues/{s.LeagueId}/seasons/{s.SeasonId}/matches");
        matches!.Length.Should().Be(2); // double round-robin of two teams
        matches.Should().OnlyContain(m => m.Status == "Proposed");
        matches.Should().OnlyContain(m => m.HomeTeamName.Length > 0 && m.AwayTeamName.Length > 0 && m.VenueName.Length > 0);
    }

    [Fact]
    public async Task Post_AsLeagueAdmin_Returns202()
    {
        var s = await ArrangeSchedulableSeason();
        var la = await Seeder.CreateUserAsync("la@example.com", "correct-horse-battery");
        await Seeder.GrantLeagueAdminAsync(s.LeagueId, la.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "la@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsync($"/api/leagues/{s.LeagueId}/seasons/{s.SeasonId}/generate", null);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Post_AsNonAdmin_Returns403()
    {
        var s = await ArrangeSchedulableSeason();
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsync($"/api/leagues/{s.LeagueId}/seasons/{s.SeasonId}/generate", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_SeasonNotDraft_Returns409()
    {
        var s = await ArrangeSchedulableSeason(status: SeasonStatus.Proposed);
        await LoginSystemAdmin();

        var response = await Client.PostAsync($"/api/leagues/{s.LeagueId}/seasons/{s.SeasonId}/generate", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Post_UnknownSeason_Returns404()
    {
        var s = await ArrangeSchedulableSeason();
        await LoginSystemAdmin();

        var response = await Client.PostAsync($"/api/leagues/{s.LeagueId}/seasons/{Guid.NewGuid()}/generate", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Infeasible_NoWeeks_RunnerFallsBackToDraftWithError()
    {
        var s = await ArrangeSchedulableSeason(withWeeks: false);
        await LoginSystemAdmin();
        await Client.PostAsync($"/api/leagues/{s.LeagueId}/seasons/{s.SeasonId}/generate", null);

        await RunSchedulerAsync();

        var season = await GetSeasonAsync(s);
        season!.Status.Should().Be("Draft");
        season.SchedulingError.Should().NotBeNullOrEmpty();
    }

    private sealed record SeasonStatusDto(Guid Id, string Status, string? SchedulingError);
    private sealed record MatchDto(
        Guid Id, Guid DivisionId, string DivisionName, Guid HomeTeamId, string HomeTeamName,
        Guid AwayTeamId, string AwayTeamName, Guid VenueId, string VenueName, string MatchDate, string Status);
}
