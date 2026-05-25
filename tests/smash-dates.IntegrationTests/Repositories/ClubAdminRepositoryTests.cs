using Microsoft.Extensions.Configuration;
using smash_dates.Data;
using smash_dates.IntegrationTests.Infrastructure;
using smash_dates.Repositories;

namespace smash_dates.IntegrationTests.Repositories;

[Collection(IntegrationTestCollection.Name)]
public sealed class ClubAdminRepositoryTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private readonly TestDataSeeder _seeder;
    private ClubAdminRepository _repo = null!;

    public ClubAdminRepositoryTests(PostgresFixture fixture)
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
        _repo = new ClubAdminRepository(new NpgsqlConnectionFactory(config));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task GrantAsync_PersistsAndIsIdempotent()
    {
        var user = await _seeder.CreateUserAsync("u@example.com", "correct-horse-battery");
        var clubId = await _seeder.CreateClubAsync("Acme", "ACME");

        await _repo.GrantAsync(clubId, user.Id, user.Id);
        await _repo.GrantAsync(clubId, user.Id, user.Id);

        (await _repo.IsAdminAsync(clubId, user.Id)).Should().BeTrue();
        (await _repo.ListByClubAsync(clubId)).Should().HaveCount(1);
    }

    [Fact]
    public async Task RevokeUnlessLastAsync_RemovesNonLast_ReturnsRevoked()
    {
        var sole = await _seeder.CreateUserAsync("a@example.com", "correct-horse-battery");
        var second = await _seeder.CreateUserAsync("b@example.com", "correct-horse-battery");
        var clubId = await _seeder.CreateClubAsync("Acme", "ACME");
        await _seeder.GrantClubAdminAsync(clubId, sole.Id, sole.Id);
        await _seeder.GrantClubAdminAsync(clubId, second.Id, sole.Id);

        var outcome = await _repo.RevokeUnlessLastAsync(clubId, second.Id);

        outcome.Should().Be(RevokeResult.Revoked);
    }

    [Fact]
    public async Task RevokeUnlessLastAsync_LastAdmin_ReturnsWouldBeLast()
    {
        var sole = await _seeder.CreateUserAsync("a@example.com", "correct-horse-battery");
        var clubId = await _seeder.CreateClubAsync("Acme", "ACME");
        await _seeder.GrantClubAdminAsync(clubId, sole.Id, sole.Id);

        var outcome = await _repo.RevokeUnlessLastAsync(clubId, sole.Id);

        outcome.Should().Be(RevokeResult.WouldBeLastAdmin);
        (await _repo.IsAdminAsync(clubId, sole.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task RevokeUnlessLastAsync_NotAGrant_ReturnsNotAdmin()
    {
        var clubId = await _seeder.CreateClubAsync("Acme", "ACME");

        var outcome = await _repo.RevokeUnlessLastAsync(clubId, Guid.NewGuid());

        outcome.Should().Be(RevokeResult.NotAdmin);
    }

    [Fact]
    public async Task RevokeAsync_ForcedDelete_AlwaysRemovesIfPresent()
    {
        var sole = await _seeder.CreateUserAsync("a@example.com", "correct-horse-battery");
        var clubId = await _seeder.CreateClubAsync("Acme", "ACME");
        await _seeder.GrantClubAdminAsync(clubId, sole.Id, sole.Id);

        var removed = await _repo.RevokeAsync(clubId, sole.Id);

        removed.Should().BeTrue();
        (await _repo.IsAdminAsync(clubId, sole.Id)).Should().BeFalse();
    }
}
