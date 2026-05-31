using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class PegboardGameEndpointsTests : IntegrationTestBase
{
    public PegboardGameEndpointsTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task StartSingles_ThenFinish_FreesCourtAndRequeuesPlayers()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);
        var sid = (await (await Client.PostAsJsonAsync($"/api/clubs/{clubId}/pegboard/sessions", new { name = "Tue" }))
            .Content.ReadFromJsonAsync<SessionRow>())!.Id;
        var courtId = (await (await Client.PostAsJsonAsync($"/api/clubs/{clubId}/pegboard/sessions/{sid}/courts", new { label = "C1" }))
            .Content.ReadFromJsonAsync<IdRow>())!.Id;
        var a1 = (await (await Client.PostAsJsonAsync($"/api/clubs/{clubId}/pegboard/sessions/{sid}/attendances", new { guestName = "Alice", gender = "Female" }))
            .Content.ReadFromJsonAsync<IdRow>())!.Id;
        var a2 = (await (await Client.PostAsJsonAsync($"/api/clubs/{clubId}/pegboard/sessions/{sid}/attendances", new { guestName = "Bob", gender = "Male" }))
            .Content.ReadFromJsonAsync<IdRow>())!.Id;

        var start = await Client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/pegboard/sessions/{sid}/games?courtId={courtId}",
            new { type = "Singles", sideA = new[] { a1 }, sideB = new[] { a2 } });
        start.StatusCode.Should().Be(HttpStatusCode.Created);
        var gameId = (await start.Content.ReadFromJsonAsync<StartRow>())!.Id;

        var finish = await Client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/pegboard/sessions/{sid}/games/{gameId}/finish",
            new { winnerSide = "A", score = "21-15" });
        finish.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var board = await Client.GetFromJsonAsync<BoardRow>($"/api/clubs/{clubId}/pegboard/sessions/{sid}/board");
        board!.Courts.Single().ActiveGame.Should().BeNull();
        board.Attendees.Should().OnlyContain(x => x.Status == "Waiting");
    }

    private sealed record SessionRow(Guid Id);
    private sealed record IdRow(Guid Id);
    private sealed record StartRow(Guid Id, bool MakeupWarning);
    private sealed record BoardRow(List<CourtRow> Courts, List<AttRow> Attendees);
    private sealed record CourtRow(Guid Id, string Label, object? ActiveGame);
    private sealed record AttRow(Guid Id, string Status);
}
