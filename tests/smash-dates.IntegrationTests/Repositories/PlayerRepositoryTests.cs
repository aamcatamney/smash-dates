using Microsoft.Extensions.Configuration;
using smash_dates.Data;
using smash_dates.IntegrationTests.Infrastructure;
using smash_dates.Repositories;

namespace smash_dates.IntegrationTests.Repositories;

[Collection(IntegrationTestCollection.Name)]
public sealed class PlayerRepositoryTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private readonly TestDataSeeder _seeder;
    private PlayerRepository _repo = null!;

    public PlayerRepositoryTests(PostgresFixture fixture)
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
        _repo = new PlayerRepository(new NpgsqlConnectionFactory(config));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task SetGrade_ThenGetById_ReturnsGrade()
    {
        var id = await _repo.CreateAsync("Pat Smith", smash_dates.Models.Gender.Female);
        await _repo.SetGradeAsync(id, 2);
        var player = await _repo.GetByIdAsync(id);
        player!.Grade.Should().Be(2);
    }
}
