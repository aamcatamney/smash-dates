using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

// Auto-rotate fill picks a valid lineup from the waiting queue and starts the game in one call.
public sealed class PegboardAutoFillEndpointTests : IntegrationTestBase
{
    public PegboardAutoFillEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private async Task<(Guid Club, Guid Session)> OpenSession()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);
        var sid = (await (await Client.PostAsJsonAsync($"/api/clubs/{clubId}/pegboard/sessions", new { name = "Tue" }))
            .Content.ReadFromJsonAsync<IdRow>())!.Id;
        return (clubId, sid);
    }

    private Task<HttpResponseMessage> AddGuest(Guid club, Guid sid, string name, string gender) =>
        Client.PostAsJsonAsync($"/api/clubs/{club}/pegboard/sessions/{sid}/attendances",
            new { guestName = name, gender });

    [Fact]
    public async Task AutoFill_WithEnoughWaiting_StartsGameAndSeatsPlayers()
    {
        var (club, sid) = await OpenSession();
        var courtId = (await (await Client.PostAsJsonAsync($"/api/clubs/{club}/pegboard/sessions/{sid}/courts", new { label = "C1" }))
            .Content.ReadFromJsonAsync<IdRow>())!.Id;
        // Four men → a valid level Doubles.
        foreach (var n in new[] { "Al", "Bo", "Cy", "Di" })
            (await AddGuest(club, sid, n, "Male")).StatusCode.Should().Be(HttpStatusCode.Created);

        var res = await Client.PostAsJsonAsync(
            $"/api/clubs/{club}/pegboard/sessions/{sid}/courts/{courtId}/auto-fill", new { type = "Doubles" });
        res.StatusCode.Should().Be(HttpStatusCode.Created);

        var board = await Client.GetFromJsonAsync<BoardRow>($"/api/clubs/{club}/pegboard/sessions/{sid}/board");
        board!.Courts.Single().ActiveGame.Should().NotBeNull();
        board.Attendees.Count(a => a.Status == "Playing").Should().Be(4);
    }

    [Fact]
    public async Task AutoFill_WithTooFewWaiting_Returns409()
    {
        var (club, sid) = await OpenSession();
        var courtId = (await (await Client.PostAsJsonAsync($"/api/clubs/{club}/pegboard/sessions/{sid}/courts", new { label = "C1" }))
            .Content.ReadFromJsonAsync<IdRow>())!.Id;
        (await AddGuest(club, sid, "Al", "Male")).EnsureSuccessStatusCode();
        (await AddGuest(club, sid, "Bo", "Male")).EnsureSuccessStatusCode();

        // Doubles needs four; only two are waiting.
        var res = await Client.PostAsJsonAsync(
            $"/api/clubs/{club}/pegboard/sessions/{sid}/courts/{courtId}/auto-fill", new { type = "Doubles" });
        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private sealed record IdRow(Guid Id);
    private sealed record BoardRow(List<CourtRow> Courts, List<AttRow> Attendees);
    private sealed record CourtRow(Guid Id, object? ActiveGame);
    private sealed record AttRow(Guid Id, string Status);
}
