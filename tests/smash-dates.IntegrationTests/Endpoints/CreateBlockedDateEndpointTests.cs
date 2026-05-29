using System.Net;
using System.Net.Http.Json;
using smash_dates.Models;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class CreateBlockedDateEndpointTests : IntegrationTestBase
{
    public CreateBlockedDateEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private async Task<Guid> ArrangeClubWithAdmin(string email = "admin@example.com")
    {
        var admin = await Seeder.CreateUserAsync(email, "correct-horse-battery");
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.GrantClubAdminAsync(clubId, admin.Id, admin.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email, password = "correct-horse-battery" });
        return clubId;
    }

    [Fact]
    public async Task Post_ClubScope_AsClubAdmin_Returns201()
    {
        var clubId = await ArrangeClubWithAdmin();

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/blocked-dates", new
        {
            scope = "Club", startDate = "2025-12-25", endDate = "2025-12-26", reason = "AGM",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_VenueScope_ValidVenue_Returns201()
    {
        var clubId = await ArrangeClubWithAdmin();
        var venueId = await Seeder.CreateVenueAsync(clubId, "Main Hall", 2);

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/blocked-dates", new
        {
            scope = "Venue", venueId, startDate = "2025-11-01", endDate = "2025-11-01", reason = "Maintenance",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_TeamScope_ValidTeam_Returns201()
    {
        var clubId = await ArrangeClubWithAdmin();
        var teamId = await Seeder.CreateTeamAsync(clubId, "Acme 1", DivisionGender.Mens);

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/blocked-dates", new
        {
            scope = "Team", teamId, startDate = "2025-10-10", endDate = "2025-10-17", reason = "Exams",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_AsSystemAdmin_Returns201()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/blocked-dates", new
        {
            scope = "Club", startDate = "2025-12-25", endDate = "2025-12-25", reason = "Holiday",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_AsNonAdmin_Returns403()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/blocked-dates", new
        {
            scope = "Club", startDate = "2025-12-25", endDate = "2025-12-25", reason = "Holiday",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_UnknownClub_Returns404()
    {
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync($"/api/clubs/{Guid.NewGuid()}/blocked-dates", new
        {
            scope = "Club", startDate = "2025-12-25", endDate = "2025-12-25", reason = "Holiday",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_EmptyReason_Returns400()
    {
        var clubId = await ArrangeClubWithAdmin();

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/blocked-dates", new
        {
            scope = "Club", startDate = "2025-12-25", endDate = "2025-12-25", reason = "   ",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_EndBeforeStart_Returns400()
    {
        var clubId = await ArrangeClubWithAdmin();

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/blocked-dates", new
        {
            scope = "Club", startDate = "2025-12-26", endDate = "2025-12-25", reason = "Oops",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_InvalidScope_Returns400()
    {
        var clubId = await ArrangeClubWithAdmin();

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/blocked-dates", new
        {
            scope = "Galaxy", startDate = "2025-12-25", endDate = "2025-12-25", reason = "Nope",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_VenueScope_MissingVenueId_Returns400()
    {
        var clubId = await ArrangeClubWithAdmin();

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/blocked-dates", new
        {
            scope = "Venue", startDate = "2025-11-01", endDate = "2025-11-01", reason = "Maintenance",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_VenueOfDifferentClub_Returns404()
    {
        var clubId = await ArrangeClubWithAdmin();
        var otherClub = await Seeder.CreateClubAsync("Beta", "BETA");
        var foreignVenue = await Seeder.CreateVenueAsync(otherClub, "Beta Hall", 1);

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/blocked-dates", new
        {
            scope = "Venue", venueId = foreignVenue, startDate = "2025-11-01", endDate = "2025-11-01", reason = "X",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_TeamOfDifferentClub_Returns404()
    {
        var clubId = await ArrangeClubWithAdmin();
        var otherClub = await Seeder.CreateClubAsync("Beta", "BETA");
        var foreignTeam = await Seeder.CreateTeamAsync(otherClub, "Beta 1", DivisionGender.Mens);

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/blocked-dates", new
        {
            scope = "Team", teamId = foreignTeam, startDate = "2025-10-10", endDate = "2025-10-17", reason = "X",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
