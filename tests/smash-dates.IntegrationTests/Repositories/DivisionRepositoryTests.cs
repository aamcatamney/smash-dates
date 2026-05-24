using Microsoft.Extensions.Configuration;
using smash_dates.Data;
using smash_dates.IntegrationTests.Infrastructure;
using smash_dates.Models;
using smash_dates.Repositories;

namespace smash_dates.IntegrationTests.Repositories;

[Collection(IntegrationTestCollection.Name)]
public sealed class DivisionRepositoryTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private readonly TestDataSeeder _seeder;
    private DivisionRepository _repo = null!;

    public DivisionRepositoryTests(PostgresFixture fixture)
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
        _repo = new DivisionRepository(new NpgsqlConnectionFactory(config));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task CreateAsync_PersistsDivision()
    {
        var admin = await _seeder.CreateSystemAdminUserAsync("admin@example.com", "correct-horse-battery");
        var leagueId = await _seeder.CreateLeagueAsync("NL", admin.Id);

        var id = await _repo.CreateAsync(leagueId, "Mens 1", DivisionGender.Mens, rank: 1, rubbersPerMatch: 9, winPoints: 2, drawPoints: 1, lossPoints: 0);

        var loaded = await _repo.GetByIdAsync(id);
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Mens 1");
        loaded.Gender.Should().Be(DivisionGender.Mens);
        loaded.Rank.Should().Be(1);
        loaded.RubbersPerMatch.Should().Be(9);
    }

    [Fact]
    public async Task ListByLeagueAsync_ReturnsDivisionsSortedByGenderThenRank()
    {
        var admin = await _seeder.CreateSystemAdminUserAsync("admin@example.com", "correct-horse-battery");
        var leagueId = await _seeder.CreateLeagueAsync("NL", admin.Id);
        await _repo.CreateAsync(leagueId, "Mens 2", DivisionGender.Mens, 2, 9, 2, 1, 0);
        await _repo.CreateAsync(leagueId, "Mens 1", DivisionGender.Mens, 1, 9, 2, 1, 0);
        await _repo.CreateAsync(leagueId, "Ladies 1", DivisionGender.Ladies, 1, 6, 2, 1, 0);

        var results = await _repo.ListByLeagueAsync(leagueId);

        results.Select(d => d.Name).Should().ContainInOrder("Ladies 1", "Mens 1", "Mens 2");
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_Throws()
    {
        var admin = await _seeder.CreateSystemAdminUserAsync("admin@example.com", "correct-horse-battery");
        var leagueId = await _seeder.CreateLeagueAsync("NL", admin.Id);
        await _repo.CreateAsync(leagueId, "Mens 1", DivisionGender.Mens, 1, 9, 2, 1, 0);

        var act = () => _repo.CreateAsync(leagueId, "MENS 1", DivisionGender.Mens, 99, 9, 2, 1, 0);

        await act.Should().ThrowAsync<Npgsql.PostgresException>();
    }
}
