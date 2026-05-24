using Microsoft.Extensions.Configuration;
using smash_dates.Data;
using smash_dates.IntegrationTests.Infrastructure;
using smash_dates.Repositories;

namespace smash_dates.IntegrationTests.Repositories;

[Collection(IntegrationTestCollection.Name)]
public sealed class LeagueRepositoryTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private readonly TestDataSeeder _seeder;
    private LeagueRepository _repo = null!;

    public LeagueRepositoryTests(PostgresFixture fixture)
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
        _repo = new LeagueRepository(new NpgsqlConnectionFactory(config));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task CreateAsync_PersistsLeague()
    {
        var admin = await _seeder.CreateUserAsync("admin@example.com", "correct-horse-battery");

        var id = await _repo.CreateAsync("North London", "Top division of NL", admin.Id);

        var loaded = await _repo.GetByIdAsync(id);
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("North London");
        loaded.Description.Should().Be("Top division of NL");
        loaded.CreatedBy.Should().Be(admin.Id);
    }

    [Fact]
    public async Task ListAsync_ReturnsAllLeagues_OrderedByName()
    {
        var admin = await _seeder.CreateUserAsync("admin@example.com", "correct-horse-battery");
        await _repo.CreateAsync("Beta", null, admin.Id);
        await _repo.CreateAsync("Alpha", null, admin.Id);

        var results = await _repo.ListAsync();

        results.Select(r => r.Name).Should().ContainInOrder("Alpha", "Beta");
    }

    [Fact]
    public async Task CreateAsync_DuplicateNameCaseInsensitive_Throws()
    {
        var admin = await _seeder.CreateUserAsync("admin@example.com", "correct-horse-battery");
        await _repo.CreateAsync("North London", null, admin.Id);

        var act = () => _repo.CreateAsync("north london", null, admin.Id);

        await act.Should().ThrowAsync<Npgsql.PostgresException>();
    }
}
