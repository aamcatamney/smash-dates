using Microsoft.Extensions.Configuration;
using smash_dates.Data;
using smash_dates.IntegrationTests.Infrastructure;
using smash_dates.Repositories;

namespace smash_dates.IntegrationTests.Repositories;

[Collection(IntegrationTestCollection.Name)]
public sealed class SessionHostRepositoryTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private readonly TestDataSeeder _seeder;
    private SessionHostRepository _repo = null!;

    public SessionHostRepositoryTests(PostgresFixture fixture)
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
        _repo = new SessionHostRepository(new NpgsqlConnectionFactory(config));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Grant_ThenIsHost_True_AndRevoke_RemovesIt()
    {
        var user = await _seeder.CreateUserAsync("host@example.com", "correct-horse-battery");
        var clubId = await _seeder.CreateClubAsync("Acme", "ACME");

        (await _repo.IsHostAsync(clubId, user.Id)).Should().BeFalse();
        await _repo.GrantAsync(clubId, user.Id, user.Id);
        (await _repo.IsHostAsync(clubId, user.Id)).Should().BeTrue();
        (await _repo.ListByClubAsync(clubId)).Should().ContainSingle(g => g.UserId == user.Id);
        (await _repo.RevokeAsync(clubId, user.Id)).Should().BeTrue();
        (await _repo.IsHostAsync(clubId, user.Id)).Should().BeFalse();
    }
}
