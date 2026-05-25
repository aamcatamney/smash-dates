using Microsoft.Extensions.Configuration;
using smash_dates.Data;
using smash_dates.IntegrationTests.Infrastructure;
using smash_dates.Repositories;

namespace smash_dates.IntegrationTests.Repositories;

[Collection(IntegrationTestCollection.Name)]
public sealed class LeagueAdminRepositoryTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private readonly TestDataSeeder _seeder;
    private LeagueAdminRepository _repo = null!;

    public LeagueAdminRepositoryTests(PostgresFixture fixture)
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
        _repo = new LeagueAdminRepository(new NpgsqlConnectionFactory(config));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task GrantAsync_PersistsGrant()
    {
        var sys = await _seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await _seeder.CreateLeagueAsync("NL", sys.Id);

        await _repo.GrantAsync(leagueId, sys.Id, grantedBy: sys.Id);

        (await _repo.IsAdminAsync(leagueId, sys.Id)).Should().BeTrue();
        (await _repo.CountByLeagueAsync(leagueId)).Should().Be(1);
    }

    [Fact]
    public async Task GrantAsync_DuplicateIsIdempotent()
    {
        var sys = await _seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await _seeder.CreateLeagueAsync("NL", sys.Id);

        await _repo.GrantAsync(leagueId, sys.Id, sys.Id);
        await _repo.GrantAsync(leagueId, sys.Id, sys.Id);

        (await _repo.CountByLeagueAsync(leagueId)).Should().Be(1);
    }

    [Fact]
    public async Task RevokeAsync_RemovesGrant_AndReturnsTrue()
    {
        var sys = await _seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await _seeder.CreateLeagueAsync("NL", sys.Id);
        await _repo.GrantAsync(leagueId, sys.Id, sys.Id);

        var revoked = await _repo.RevokeAsync(leagueId, sys.Id);

        revoked.Should().BeTrue();
        (await _repo.IsAdminAsync(leagueId, sys.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task RevokeAsync_NonExistent_ReturnsFalse()
    {
        var sys = await _seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await _seeder.CreateLeagueAsync("NL", sys.Id);

        var revoked = await _repo.RevokeAsync(leagueId, Guid.NewGuid());

        revoked.Should().BeFalse();
    }
}
