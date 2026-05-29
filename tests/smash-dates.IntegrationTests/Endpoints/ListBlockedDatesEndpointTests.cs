using System.Net;
using System.Net.Http.Json;
using smash_dates.Models;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class ListBlockedDatesEndpointTests : IntegrationTestBase
{
    public ListBlockedDatesEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record BlockedDateDto(
        Guid Id, Guid ClubId, string Scope, Guid? VenueId, Guid? TeamId, string StartDate, string EndDate, string Reason);

    [Fact]
    public async Task Get_ReturnsAllScopesForClub_AnyAuthenticatedUser()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        var venueId = await Seeder.CreateVenueAsync(clubId, "Main Hall", 1);
        var teamId = await Seeder.CreateTeamAsync(clubId, "Acme 1", DivisionGender.Mens);
        await Seeder.CreateBlockedDateAsync(clubId, BlockedDateScope.Club, new DateOnly(2025, 12, 25), new DateOnly(2025, 12, 26), "AGM");
        await Seeder.CreateBlockedDateAsync(clubId, BlockedDateScope.Venue, new DateOnly(2025, 11, 1), new DateOnly(2025, 11, 1), "Maint", venueId: venueId);
        await Seeder.CreateBlockedDateAsync(clubId, BlockedDateScope.Team, new DateOnly(2025, 10, 10), new DateOnly(2025, 10, 17), "Exams", teamId: teamId);
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var blocks = await Client.GetFromJsonAsync<BlockedDateDto[]>($"/api/clubs/{clubId}/blocked-dates");

        blocks.Should().NotBeNull();
        blocks!.Length.Should().Be(3);
    }

    [Fact]
    public async Task Get_Unauthenticated_Returns401()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");

        var response = await Client.GetAsync($"/api/clubs/{clubId}/blocked-dates");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
