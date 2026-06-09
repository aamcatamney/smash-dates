using System.Net.Http;

namespace smash_dates.IntegrationTests.Infrastructure;

[Collection(IntegrationTestCollection.Name)]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected PostgresFixture Fixture { get; }
    protected TestWebApplicationFactory Factory { get; private set; } = null!;
    protected HttpClient Client { get; private set; } = null!;
    protected TestDataSeeder Seeder { get; }

    protected IntegrationTestBase(PostgresFixture fixture)
    {
        Fixture = fixture;
        Seeder = new TestDataSeeder(fixture.ConnectionString);
    }

    public async ValueTask InitializeAsync()
    {
        await Fixture.ResetAsync();
        // Reuse the collection-wide factory; only the per-test HTTP client (and its cookies) is
        // fresh. The factory is owned by the fixture, so we don't dispose it here.
        Factory = Fixture.Factory;
        Client = Factory.CreateClient();
    }

    public ValueTask DisposeAsync()
    {
        Client?.Dispose();
        return ValueTask.CompletedTask;
    }
}
