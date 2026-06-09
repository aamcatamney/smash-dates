using smash_dates.Migrations;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace smash_dates.IntegrationTests.Infrastructure;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("smash_dates")
        .WithUsername("postgres")
        .WithPassword("postgres")
        // One shared WebApplicationFactory (and Npgsql pool) serves the whole suite, but
        // headroom above the default 100 cap keeps back-to-back tests off connection limits.
        .WithCommand("-c", "max_connections=500")
        .Build();

    private Respawner? _respawner;

    public string ConnectionString => _container.GetConnectionString();

    // One factory for the whole collection: building the host (DI graph + startup migration)
    // is the dominant per-test cost, so we pay it once instead of ~once per test. The app is
    // stateless between requests (state lives in Postgres, reset by Respawn), so sharing is safe.
    public TestWebApplicationFactory Factory { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        DbMigrator.Apply(ConnectionString);

        Factory = new TestWebApplicationFactory(ConnectionString);

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore =
            [
                new Respawn.Graph.Table("public", "schemaversions"),
                new Respawn.Graph.Table("public", "data_protection_keys"),
            ],
        });
    }

    public async Task ResetAsync()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await _respawner!.ResetAsync(conn);
    }

    public async ValueTask DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }

        await _container.DisposeAsync();
    }
}
