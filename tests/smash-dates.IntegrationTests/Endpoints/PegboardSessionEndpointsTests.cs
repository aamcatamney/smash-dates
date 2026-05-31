using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class PegboardSessionEndpointsTests : IntegrationTestBase
{
    public PegboardSessionEndpointsTests(PostgresFixture fixture) : base(fixture) { }

    private async Task<Guid> LoginSysAdminAndClub()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);
        return clubId;
    }

    [Fact]
    public async Task Open_AsSystemAdmin_Returns201_ThenSecondOpen_Returns409()
    {
        var clubId = await LoginSysAdminAndClub();
        var first = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/pegboard/sessions", new { name = "Tuesday" });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/pegboard/sessions", new { name = "Wednesday" });
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Close_ThenMutation_Returns409()
    {
        var clubId = await LoginSysAdminAndClub();
        var open = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/pegboard/sessions", new { name = "Tuesday" });
        var session = await open.Content.ReadFromJsonAsync<SessionRow>();

        var close = await Client.PostAsync($"/api/clubs/{clubId}/pegboard/sessions/{session!.Id}/close", null);
        close.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Mutating a closed session is a 409. AddCourt isn't mapped until Task 13b, so assert
        // the conflict against the close endpoint itself (closing an already-closed session).
        // TODO(13b): also assert 409 on add-court after close
        var closeAgain = await Client.PostAsync($"/api/clubs/{clubId}/pegboard/sessions/{session.Id}/close", null);
        closeAgain.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private sealed record SessionRow(Guid Id, Guid ClubId, string Name, string Status);
}
