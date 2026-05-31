using Microsoft.Extensions.Configuration;
using smash_dates.Data;
using smash_dates.IntegrationTests.Infrastructure;
using smash_dates.Models;
using smash_dates.Repositories;

namespace smash_dates.IntegrationTests.Repositories;

[Collection(IntegrationTestCollection.Name)]
public sealed class PegboardRepositoryTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private readonly TestDataSeeder _seeder;
    private PegboardRepository _repo = null!;

    public PegboardRepositoryTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _seeder = new TestDataSeeder(fixture.ConnectionString);
    }

    public async ValueTask InitializeAsync()
    {
        await _fixture.ResetAsync();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _fixture.ConnectionString,
            })
            .Build();
        _repo = new PegboardRepository(new NpgsqlConnectionFactory(config));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task FullSpine_Open_Court_Attend_Play_Finish_UpdatesBoardAndStats()
    {
        var clubId = await _seeder.CreateClubAsync("Acme", "ACME");
        var opener = await _seeder.CreateUserAsync("h@example.com", "correct-horse-battery");

        var sessionId = await _repo.OpenAsync(clubId, "Tuesday Club Night", opener.Id);
        var courtId = await _repo.AddCourtAsync(sessionId, "Court 1");

        var a1 = await _repo.AddGuestAttendanceAsync(sessionId, "Alice", Gender.Female, 2);
        var a2 = await _repo.AddGuestAttendanceAsync(sessionId, "Bob", Gender.Male, 3);

        var gameId = await _repo.StartGameAsync(sessionId, courtId, GameType.Singles, [a1], [a2]);

        var mid = await _repo.GetBoardAsync(sessionId);
        mid!.Courts.Single().ActiveGame!.Players.Should().HaveCount(2);
        mid.Attendees.Should().OnlyContain(x => x.Status == AttendanceStatus.Playing);

        (await _repo.FinishGameAsync(gameId, GameSide.A, "21-15")).Should().BeTrue();

        var after = await _repo.GetBoardAsync(sessionId);
        after!.Courts.Single().ActiveGame.Should().BeNull();
        after.Attendees.Should().OnlyContain(x => x.Status == AttendanceStatus.Waiting);
        after.Attendees.Single(x => x.Id == a1).GamesWon.Should().Be(1);
        after.Attendees.Single(x => x.Id == a2).GamesWon.Should().Be(0);
        after.Attendees.Should().OnlyContain(x => x.GamesPlayed == 1);
    }

    [Fact]
    public async Task Open_SecondOpenSession_SameClub_Throws()
    {
        var clubId = await _seeder.CreateClubAsync("Acme", "ACME");
        var u = await _seeder.CreateUserAsync("h2@example.com", "correct-horse-battery");
        await _repo.OpenAsync(clubId, "First", u.Id);

        var act = async () => await _repo.OpenAsync(clubId, "Second", u.Id);
        await act.Should().ThrowAsync<Npgsql.PostgresException>();
    }
}
