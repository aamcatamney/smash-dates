using Microsoft.Extensions.Configuration;
using smash_dates.Data;
using smash_dates.IntegrationTests.Infrastructure;
using smash_dates.Repositories;

namespace smash_dates.IntegrationTests.Repositories;

[Collection(IntegrationTestCollection.Name)]
public sealed class ClubRepositoryTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private readonly TestDataSeeder _seeder;
    private ClubRepository _repo = null!;

    public ClubRepositoryTests(PostgresFixture fixture)
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
        _repo = new ClubRepository(new NpgsqlConnectionFactory(config));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task CreateWithFirstAdminAsync_PersistsClubAndGrant()
    {
        var admin = await _seeder.CreateUserAsync("admin@example.com", "correct-horse-battery");
        var id = await _repo.CreateWithFirstAdminAsync(
            "Acme Badminton Club", "ACME", "contact@acme.test", "private notes",
            firstAdminUserId: admin.Id, grantedBy: admin.Id);

        var loaded = await _repo.GetByIdAsync(id);
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Acme Badminton Club");
        loaded.ShortCode.Should().Be("ACME");
        loaded.ContactEmail.Should().Be("contact@acme.test");
        loaded.Notes.Should().Be("private notes");
    }

    [Fact]
    public async Task ListAsync_ReturnsAllClubs_OrderedByName()
    {
        await _seeder.CreateClubAsync("Beta", "BETA");
        await _seeder.CreateClubAsync("Alpha", "ALPHA");

        var results = await _repo.ListAsync();

        results.Select(c => c.Name).Should().ContainInOrder("Alpha", "Beta");
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var id = await _seeder.CreateClubAsync("Acme", "ACME");

        var updated = await _repo.UpdateAsync(id, "Acme Renamed", "ACMER", "new@acme.test", "fresh notes");

        updated.Should().BeTrue();
        var loaded = await _repo.GetByIdAsync(id);
        loaded!.Name.Should().Be("Acme Renamed");
        loaded.ShortCode.Should().Be("ACMER");
        loaded.ContactEmail.Should().Be("new@acme.test");
        loaded.Notes.Should().Be("fresh notes");
    }

    [Fact]
    public async Task CreateWithFirstAdminAsync_DuplicateShortCode_Throws()
    {
        var admin = await _seeder.CreateUserAsync("admin@example.com", "correct-horse-battery");
        await _seeder.CreateClubAsync("First", "ACME");

        var act = () => _repo.CreateWithFirstAdminAsync(
            "Second", "ACME", "x@y.test", null, admin.Id, admin.Id);

        await act.Should().ThrowAsync<Npgsql.PostgresException>();
    }
}
