using System.Net;
using System.Net.Http.Json;
using smash_dates.Models;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class CreateSeasonEntryEndpointTests : IntegrationTestBase
{
    public CreateSeasonEntryEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record Setup(Guid LeagueId, Guid SeasonId, Guid DivisionId, Guid ClubId, Guid TeamId);

    // Builds a fully valid arrangement: a Draft season, a Mens division, a club with an
    // Accepted membership in the league, and a Mens team in that club. SystemAdmin seeded.
    private async Task<Setup> ArrangeValidAsync(
        DivisionGender divisionGender = DivisionGender.Mens,
        DivisionGender teamGender = DivisionGender.Mens,
        SeasonStatus seasonStatus = SeasonStatus.Draft,
        MembershipStatus membership = MembershipStatus.Accepted)
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        var seasonId = await Seeder.CreateSeasonAsync(leagueId, "2025/26", new DateOnly(2025, 9, 1), new DateOnly(2026, 4, 30), seasonStatus);
        var divisionId = await Seeder.CreateDivisionAsync(leagueId, "Div 1", divisionGender, 1, 9);
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.CreateMembershipAsync(clubId, leagueId, membership);
        var teamId = await Seeder.CreateTeamAsync(clubId, "Acme 1", teamGender);
        return new Setup(leagueId, seasonId, divisionId, clubId, teamId);
    }

    private async Task LoginSystemAdmin() =>
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

    [Fact]
    public async Task Post_ValidEntry_AsSystemAdmin_Returns201()
    {
        var s = await ArrangeValidAsync();
        await LoginSystemAdmin();

        var response = await Client.PostAsJsonAsync(
            $"/api/leagues/{s.LeagueId}/seasons/{s.SeasonId}/entries",
            new { teamId = s.TeamId, divisionId = s.DivisionId });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_AsLeagueAdmin_Returns201()
    {
        var s = await ArrangeValidAsync();
        var la = await Seeder.CreateUserAsync("la@example.com", "correct-horse-battery");
        await Seeder.GrantLeagueAdminAsync(s.LeagueId, la.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "la@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsJsonAsync(
            $"/api/leagues/{s.LeagueId}/seasons/{s.SeasonId}/entries",
            new { teamId = s.TeamId, divisionId = s.DivisionId });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_AsNonAdmin_Returns403()
    {
        var s = await ArrangeValidAsync();
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync(
            $"/api/leagues/{s.LeagueId}/seasons/{s.SeasonId}/entries",
            new { teamId = s.TeamId, divisionId = s.DivisionId });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_UnknownSeason_Returns404()
    {
        var s = await ArrangeValidAsync();
        await LoginSystemAdmin();

        var response = await Client.PostAsJsonAsync(
            $"/api/leagues/{s.LeagueId}/seasons/{Guid.NewGuid()}/entries",
            new { teamId = s.TeamId, divisionId = s.DivisionId });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_GenderMismatch_Returns400()
    {
        var s = await ArrangeValidAsync(divisionGender: DivisionGender.Mens, teamGender: DivisionGender.Ladies);
        await LoginSystemAdmin();

        var response = await Client.PostAsJsonAsync(
            $"/api/leagues/{s.LeagueId}/seasons/{s.SeasonId}/entries",
            new { teamId = s.TeamId, divisionId = s.DivisionId });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_DivisionOfDifferentLeague_Returns404()
    {
        var s = await ArrangeValidAsync();
        var otherLeague = await Seeder.CreateLeagueAsync("Other", (await Seeder.CreateUserAsync("x@example.com", "correct-horse-battery")).Id);
        var foreignDivision = await Seeder.CreateDivisionAsync(otherLeague, "Foreign", DivisionGender.Mens, 1, 9);
        await LoginSystemAdmin();

        var response = await Client.PostAsJsonAsync(
            $"/api/leagues/{s.LeagueId}/seasons/{s.SeasonId}/entries",
            new { teamId = s.TeamId, divisionId = foreignDivision });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_ClubNotAcceptedMember_Returns409()
    {
        var s = await ArrangeValidAsync(membership: MembershipStatus.Pending);
        await LoginSystemAdmin();

        var response = await Client.PostAsJsonAsync(
            $"/api/leagues/{s.LeagueId}/seasons/{s.SeasonId}/entries",
            new { teamId = s.TeamId, divisionId = s.DivisionId });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Post_DuplicateTeamInSeason_Returns409()
    {
        var s = await ArrangeValidAsync();
        var division2 = await Seeder.CreateDivisionAsync(s.LeagueId, "Div 2", DivisionGender.Mens, 2, 9);
        await LoginSystemAdmin();
        await Client.PostAsJsonAsync(
            $"/api/leagues/{s.LeagueId}/seasons/{s.SeasonId}/entries",
            new { teamId = s.TeamId, divisionId = s.DivisionId });

        var dup = await Client.PostAsJsonAsync(
            $"/api/leagues/{s.LeagueId}/seasons/{s.SeasonId}/entries",
            new { teamId = s.TeamId, divisionId = division2 });

        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Post_SeasonNotDraft_Returns409()
    {
        var s = await ArrangeValidAsync(seasonStatus: SeasonStatus.Active);
        await LoginSystemAdmin();

        var response = await Client.PostAsJsonAsync(
            $"/api/leagues/{s.LeagueId}/seasons/{s.SeasonId}/entries",
            new { teamId = s.TeamId, divisionId = s.DivisionId });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Post_UnknownTeam_Returns404()
    {
        var s = await ArrangeValidAsync();
        await LoginSystemAdmin();

        var response = await Client.PostAsJsonAsync(
            $"/api/leagues/{s.LeagueId}/seasons/{s.SeasonId}/entries",
            new { teamId = Guid.NewGuid(), divisionId = s.DivisionId });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
